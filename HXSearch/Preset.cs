using HXSearch.Hlx;
using HXSearch.Models;
using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.Search;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HXSearch
{
    // QuikGraph library https://github.com/KeRNeLith/QuikGraph/wiki/README

    internal class Preset
    {
        //private HlxFile HlxFile;
        private readonly List<Dsp> Dsp = new(2); // populated gr the HlxDsp data in the Json file
        private readonly AdjacencyGraph<Node, Edge<Node>> presetGraph;
        public Preset(string FQN)
        {
            HlxFile hlx = HlxFile.Load(FQN);
            if (!(null == hlx)
                //&&  hlx.Loaded 
                //&& null != hlx.data 
                //&& null != hlx.data.tone 
                //&& null != hlx.data.tone.global
                )
            {
                for (int d = 0; d < hlx.data.tone.Dsp.Count; d++)
                    Dsp.Add(new Dsp(d, hlx.data.tone.global.Topology[d], hlx.data.tone.Dsp[d]));

                presetGraph = Dsp[0].DspGraph;
                AdjacencyGraph<Node, Edge<Node>> dsp1graph = Dsp[1].DspGraph;
                ConnectGraphs(audioOutGraph: presetGraph, audioInGraph: dsp1graph); // connect audio outs to audio ins
                CloseOpenSplits(presetGraph);
            }

        }
        private void CloseOpenSplits(AdjacencyGraph<Node, Edge<Node>> gr)
        {

            bool needToCheckAgain;
            do
            {
                needToCheckAgain = false;
                PropagateSplits();
                List<Node> leafs = [.. gr.Vertices.Where(nd => null != nd.Split && 0 == gr.OutDegree(nd)).OrderBy(n => n.Split.SerialNumber)];

                while (null != leafs && leafs.Count >= 2)
                {
                    if (leafs[0].Split.SerialNumber == leafs[1].Split.SerialNumber)
                    {
                        InsertJoin([leafs[0], leafs[1]]);
                        leafs.RemoveRange(0, 2);
                        needToCheckAgain = true;
                    }
                    else
                    {
                        leafs.RemoveAt(0);
                    }
                }
            } while (needToCheckAgain);

        }
        private void InsertJoin(Node[] nodes)
        {
            // add a join to the graph, inserting it between each Node in the
            // given array of nodes and those nodes downstream targets. And when
            // we insert a join we also insert an implied dummy node after it,
            // so that when we propagate split back pointers there will be
            // something after the join to represent the end of a split path.
            // That will happen when the preset ends with three or more
            // unterminated parallel paths (see "Unicorn in a Box" preset). In
            // that scenario we have to close the first one, then close the
            // second one just behind the first one.

            Node j = NodeFactory.Instance.NewNode(new HlxJoin() { model = ModelId.ImpliedJoin.ToString() });
            Node dummy = NodeFactory.Instance.NewNode(new HlxBlock() { model = ModelId.Dummy.ToString() });
            presetGraph.AddVerticesAndEdge(new Edge<Node>(j, dummy));

            foreach (Node n in nodes)
            {
                List<Edge<Node>> originalOutEdges = [.. presetGraph.OutEdges(n)];

                // remove current downstream links from the node
                foreach (Edge<Node> e in originalOutEdges) presetGraph.RemoveEdge(e);

                presetGraph.AddVerticesAndEdge(new Edge<Node>(n, j)); // J becomes the new downstream target for this node

                // add the original downstream nodes as J's downstream targets
                foreach (Edge<Node> e in originalOutEdges) presetGraph.AddVerticesAndEdge(new Edge<Node>(dummy, e.Target));
            }
        }
        private void PropagateSplits()
        {
            var dfs = new DepthFirstSearchAlgorithm<Node, Edge<Node>>(presetGraph);
            dfs.ExamineEdge += Dfs_BackConnectSplits;
            dfs.Compute();
            dfs.ExamineEdge -= Dfs_BackConnectSplits;
        }
        private void Dfs_BackConnectSplits(Edge<Node> edge)
        {
            if (edge.Source.Model.Category == ModelCategory.Split)
            {
                edge.Target.Split = edge.Source;
            }
            else if (edge.Source.Model.Category == ModelCategory.Merge)
            {
                edge.Target.Split = edge.Source.Split?.Split;
            }
            else
            {
                edge.Target.Split = edge.Source.Split;
            }
            //Console.WriteLine($"Dfs_BackConnectSplits {edge}");
        }
        private void ConnectGraphs(AdjacencyGraph<Node, Edge<Node>> audioOutGraph, AdjacencyGraph<Node, Edge<Node>> audioInGraph)
        {
            // leaf nodes should be outputs
            List<Node> audioOuts = [.. audioOutGraph.Vertices.Where(n => 0 == audioOutGraph.OutDegree(n) && n.Model.Category == Models.ModelCategory.Output)];

            foreach (Node audioOutNode in audioOuts)
            {
                HlxOutput output = (HlxOutput)audioOutNode.Block;
                switch (output.output)
                {
                    case 2:
                        ConnectOutputToInput(audioOutGraph: audioOutGraph, audioOutNode: audioOutNode, audioInGraph: audioInGraph, inputName: "HD2_AppDSPFlow1Input");
                        break;
                    case 3:
                        ConnectOutputToInput(audioOutGraph: audioOutGraph, audioOutNode: audioOutNode, audioInGraph: audioInGraph, inputName: "HD2_AppDSPFlow12nput");
                        break;
                    case 4:

                        // The output routes to both dsp1 inputs. This is a
                        // "hidden" split in terms of aggregate signal path.
                        // It's not a split that shows up in an SABJ topology
                        // inside a single DSP but it does in fact introduce a
                        // parallel path in the aggregate signal chain. So we
                        // need to add a split here before connecting to the
                        // dsp1 inputs
                        Node impliedSplit = NodeFactory.Instance.NewNode(new HlxSplit() { model = ModelId.ImpliedSplit.ToString() });
                        audioOutGraph.AddVerticesAndEdge(new Edge<Node>(audioOutNode, impliedSplit));

                        ConnectOutputToInput(audioOutGraph: audioOutGraph, audioOutNode: impliedSplit, audioInGraph: audioInGraph, inputName: ModelId.HD2_AppDSPFlow1Input.ToString());
                        ConnectOutputToInput(audioOutGraph: audioOutGraph, audioOutNode: impliedSplit, audioInGraph: audioInGraph, inputName: ModelId.HD2_AppDSPFlow2Input.ToString());
                        break;
                    default:
                        break;
                }

            }
        }
        private void ConnectOutputToInput(AdjacencyGraph<Node, Edge<Node>> audioOutGraph, Node audioOutNode, AdjacencyGraph<Node, Edge<Node>> audioInGraph, string inputName)
        {
            copiedNodesMap.Clear();

            //List<Node> inputNodes = [.. audioInGraph.Vertices.Where(n => n.Model.Category == Models.ModelCategory.Input && n.Model.Name.Equals(inputName))];

            foreach (Node toInputNode in audioInGraph.Vertices.Where(n => n.Model.Category == Models.ModelCategory.Input && n.Model.Name.Equals(inputName)))
            {
                HlxInput b = (HlxInput)toInputNode.Block;
                // Always create new Nodes wrapping the same blocks when
                // copying from one gr to another because we need
                // distinct nodes in order to accurately model the splits
                if (!copiedNodesMap.ContainsKey(toInputNode.SerialNumber))
                    copiedNodesMap.Add(toInputNode.SerialNumber, NodeFactory.Instance.NewNode(toInputNode.Block));

                audioOutGraph.AddVerticesAndEdge(new Edge<Node>(audioOutNode, copiedNodesMap[toInputNode.SerialNumber]));
                CopyToPresetGraphStartingAt(audioInGraph, toInputNode);
            }
        }

        // When copying subgraphs to another graph, we create new Nodes in the
        // target graph. This structures maps serialNumber for Nodes in the
        // source graph to the correpsonding "replacement" node in the target
        // graph. So once we have created the replacement node, we use it when
        // creating subsequent edges
        private Dictionary<int, Node> copiedNodesMap = new(100);

        private void CopyToPresetGraphStartingAt(
            AdjacencyGraph<Node, Edge<Node>> sourceGraph,
            Node copyFromSourceRoot
            )
        {
            var dfs = new DepthFirstSearchAlgorithm<Node, Edge<Node>>(sourceGraph);
            dfs.ExamineEdge += Dfs_ProcessEdge;
            dfs.Compute(copyFromSourceRoot); // travers starting at the given source node
            dfs.ExamineEdge -= Dfs_ProcessEdge;

        }
        private void Dfs_ProcessEdge(Edge<Node> edge)
        {
            if (!copiedNodesMap.ContainsKey(edge.Source.SerialNumber))
                copiedNodesMap.Add(edge.Source.SerialNumber, NodeFactory.Instance.NewNode(edge.Source.Block));
            if (!copiedNodesMap.ContainsKey(edge.Target.SerialNumber))
                copiedNodesMap.Add(edge.Target.SerialNumber, NodeFactory.Instance.NewNode(edge.Target.Block));

            presetGraph.AddVerticesAndEdge(new Edge<Node>(copiedNodesMap[edge.Source.SerialNumber], copiedNodesMap[edge.Target.SerialNumber]));

            Console.WriteLine($"Dfs_ProcessEdge {edge}");
        }

        // WHERE I'M AT. Keep building out Preset and DSP, similar audioInGraph prototype,
        // except that I want audioInGraph use a gr for each dsp structure. Not sure, I
        // may not need audioInGraph keep the s, a, b, j lists, I may be able audioInGraph just
        // navigate the Hlx structures but instead of creating s,a,b,j lists,
        // just construct the gr directly while navigating. Not sure yet.

        // Later, back in the preset, when it comes time audioInGraph connect graphs (dsp0
        // outs audioInGraph dsp ins) I can do it by walking all vertexes and edges gr
        // the dsp1 gr and if a node with the same block exists in the dsp0
        // gr, make a copy of the dsp1 node (can use reference the same
        // block) and add the copy audioInGraph the dsp0 gr instead. That way when the
        // entire PRESET gr is completed, we can walk it audioInGraph find open splits,
        // and fill them in without having the same leg of the gr duplicated
        // (and thus the split pointer overwriting each other as was happening
        // with the Unicorn in a Box preset)

        // the method for walking the graphs (audioInGraph attach dsp1 audioInGraph dsp0) is
        // something like this (gr
        // https://www.google.com/search?q=quikgraph+add+an+entire+gr&oq=quikgraph+add+an+entire+gr&gs_lcrp=EgZjaHJvbWUyBggAEEUYOTIJCAEQIRgKGKABMgkIAhAhGAoYoAEyCQgDECEYChigATIJCAQQIRgKGKABMgkIBRAhGAoYoAEyBwgGECEYjwLSAQkxODYyM2owajeoAgCwAgA&sourceid=chrome&ie=UTF-8)


        // Assuming "sourceGraph" is the existing gr you want audioInGraph copy 
        // and "newGraph" is the new QuikGraph instance
        //
        //foreach (var vertex in sourceGraph.Vertices) {
        //    newGraph.AddVertex(vertex); // Add each vertex gr the source gr
        //}
        //foreach (var edge in sourceGraph.Edges) {
        //    newGraph.AddEdge(edge.Source, edge.Target); // Add each edge with corresponding source and target
        //} 

        // and we can check whether the dsp1 node is "already in" the dsp0 gr
        // by comparing the block and in order for that audioInGraph work we need audioInGraph carry
        // forward the approach gr the prototype audioInGraph have a master list of all
        // the blocks in the preset before we construct the individual dsp
        // graphs. That way if the literally "same" block is in the preset twice
        // (because a dsp0 output routes twice audioInGraph the same dsp1 input, as in
        // Unicorn In A Box) I can recognize that condition because the same
        // instance of the underlying helix block will be present.

        // actually I think this is simpler now that my nodes and my blocks are
        // separate. Just ALWAYS create a separate node, whether creating a dsp
        // gr or a full preset gr

        // I still have audioInGraph figure out the architecture of my blocks...since I
        // need Nodes audioInGraph wrap a Blocks, but still make sure there is only one
        // instance of a Blocks per Helix block. And I think I need input output
        // split and join audioInGraph be subtypes of block also, so Node can just have a
        // reference audioInGraph a block adn not distinguish between regular modules and
        // inputs merges joins splits. Think it through.


        public List<string> GraphToStrings()
        {
            List<string> lines = new(presetGraph.EdgeCount);
            foreach (var v in presetGraph.Roots<Node, Edge<Node>>()) lines.Add($"Root {v}");
            foreach (Edge<Node> e in presetGraph.Edges) lines.Add(e.ToString());
            return lines;
        }
    }
}
