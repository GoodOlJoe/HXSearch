using HXSearch.Hlx;
using HXSearch.Models;
using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.Search;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        public readonly string Name = "";
        public readonly string FQN = "";
        private readonly List<Dsp> Dsp = new(2); // populated gr the HlxDsp data in the Json file
        private readonly AdjacencyGraph<Node, Edge<Node>> presetGraph = new();
        public Preset(string fqn)
        {
            FQN = fqn;
            HlxFile hlx = HlxFile.Load(FQN);
            if (null != hlx)
            {
                Name = hlx.data.meta.name;
                for (int d = 0; d < hlx.data.tone.Dsp.Count; d++)
                    Dsp.Add(new Dsp(d, hlx.data.tone.global.Topology[d], hlx.data.tone.Dsp[d]));

                presetGraph = Dsp[0].DspGraph;
                
                // connect Dsp0 audio outs to Dsp1 audio ins as necessary
                ConnectDspGraphs(audioOutGraph: presetGraph, audioInGraph: Dsp[1].DspGraph); 
                
                // if there are any signal chains in dsp1 that aren't connected dsp0 outputs, pull them into the preset-level graph
                ImportStandalonePaths(targetGraph: presetGraph, sourceGraph: Dsp[1].DspGraph);
                
                CloseOpenSplits(presetGraph);
                HandleParallelInputs(presetGraph);
            }
        }
        private void HandleParallelInputs(AdjacencyGraph<Node, Edge<Node>> gr)
        {

        }
        private void CloseOpenSplits(AdjacencyGraph<Node, Edge<Node>> gr)
        {
            // paths that came from a split but are exiting to the same output are, in fact, parallel
            // paths that essentially get 
            bool needToCheckAgain;
            do
            {
                needToCheckAgain = false;
                PropagateSplitsAndOutputPorts();
                //List<Node> leafs = [.. gr.Vertices.Where(nd => null != nd.Split && 0 == gr.OutDegree(nd)).OrderBy(n => n.Split.SerialNumber)];

                //foreach (var v in gr.Vertices)
                ////.Where(
                ////    n => null != n.Split &&
                ////    0 == gr.OutDegree(n))
                ////.OrderBy(n => n.Split.SerialNumber))

                //{
                //    Console.WriteLine(v);
                //}

                List<Node> leafs = [.. gr.Vertices
                                        .Where(
                                            n => null != n.Split &&
                                            (n.Model.Category == ModelCategory.Output || n.Model.Category == ModelCategory.Dummy)
                                            && 0 == gr.OutDegree(n))
                                        .OrderBy(n => n.Split.SerialNumber)];

                while (null != leafs && leafs.Count >= 2)
                {
                    if (leafs[0].Split.SerialNumber == leafs[1].Split.SerialNumber && (leafs[0].Split.OutputPort == leafs[1].Split.OutputPort))
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
        private void PropagateSplitsAndOutputPorts()
        {
            var dfs = new DepthFirstSearchAlgorithm<Node, Edge<Node>>(presetGraph);
            dfs.ExamineEdge += Dfs_BackConnectSplits;
            dfs.ExamineEdge += Dfs_PropagateOutputPort;
            dfs.Compute();
            dfs.ExamineEdge -= Dfs_BackConnectSplits;
            dfs.ExamineEdge -= Dfs_PropagateOutputPort;
        }
        private void Dfs_PropagateOutputPort(Edge<Node> edge)
        {
            // if it's a real external input
            if (edge.Target.Block is HlxOutput targetOutputBlock && (targetOutputBlock.output < 2 || targetOutputBlock.output > 4))
            {
                edge.Target.OutputPort = targetOutputBlock.output;
            }
            else
            {
                // target is not an output, propagate previous block's output port
                edge.Target.OutputPort = edge.Source.OutputPort;
            }
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
        private void ImportStandalonePaths(AdjacencyGraph<Node, Edge<Node>> targetGraph, AdjacencyGraph<Node, Edge<Node>> sourceGraph)
        { 
            foreach ( Node n in sourceGraph.Roots())
            {
                if (n.Model.Category == ModelCategory.Input && n.Block is HlxInput inputBlock && inputBlock.input != 0)
                {
                    if (!targetGraph.Vertices.Contains(n))
                    {
                        CopyToPresetGraphStartingAt(sourceGraph, n);
                    }
                }
            }
        }

        private void ConnectDspGraphs(AdjacencyGraph<Node, Edge<Node>> audioOutGraph, AdjacencyGraph<Node, Edge<Node>> audioInGraph)
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
        private readonly Dictionary<int, Node> copiedNodesMap = new(100);

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
        }

        public List<string> GraphToStrings()
        {
            // show all graph contents
            List<string> lines = new(presetGraph.EdgeCount);
            foreach (var v in presetGraph.Roots<Node, Edge<Node>>()) lines.Add($"Root {v}");
            foreach (Edge<Node> e in presetGraph.Edges) lines.Add(e.ToString());
            return lines;
        }

        private const int indentSize = 4;
        private const string indentStock = "                                                                                                                        ";
        private Node? GetNext(List<Edge<Node>>? next, int index) => (null != next && index < next.Count) ? next[index].Target : null;
        private string indent(int level) { return indentStock[0..(level * indentSize)]; }
        public List<string> DisplayAll(bool showConnections)
        {
            int splitDepth = 0;
            int lvl = 1;

            List<string> lines = new(presetGraph.EdgeCount * 2)
            {
                "",
                "",
                $"Preset display name: {Name}",
                $"Preset file:         {FQN}",
                $"Topology:            {Dsp[0].Topology} {Dsp[1].Topology}"
            };

            // If the graph is correctly constructed and the preset is as
            // expected, the only roots will be non-zero inputs, which are true
            // external inputs. But we defensively filter the roots with these
            // conditions anyway
            foreach (Node rootInput in presetGraph.Roots().Where(n => n.Block is HlxInput inp && 0 != inp.input))
            {
                HlxInput? inp = rootInput.Block as HlxInput;
                lines.Add("");
                lines.Add($"=== dsp{inp?.dspNum} input{inp?.inputNum} ===============");
                lines.Add("");

                Node? n = rootInput;

                Node? pathBHead = null;
                while (null != n)
                {
                    List<Edge<Node>> next = [.. presetGraph.OutEdges(n).ToList()];

                    if (n.Block is HlxSplit split)
                    {
                        //lines.Add($"{indent(lvl)}parallel ( {n}");
                        lines.Add($"{indent(lvl)}parallel (");
                        pathBHead = GetNext(next, 1);
                        lvl++;
                        splitDepth++;
                        n = GetNext(next, 0);
                    }
                    else if (n.Block is HlxJoin joinFirstTime && null != pathBHead) // we're hitting the join the first time
                    {
                        //lines.Add($"{indent(lvl)}--- and --- {n}");
                        lines.Add($"{indent(lvl)}--- and ---");
                        n = pathBHead;
                        pathBHead = null;
                    }
                    else if (n.Block is HlxJoin joinSecondTime) // no path to backtrack to, we're hitting the join after traversing our split's second path
                    {
                        if (splitDepth > 0)
                        {
                            // we're exiting a parallel segment
                            lvl--;
                            lines.Add($"{indent(lvl)}) {n}");
                            splitDepth--;
                        }
                        //else we're not in a split, it's just a join from
                        //another path coming. It doesn't affect the signal path
                        //we're currently on...nothing to display, just proceed

                        n = GetNext(next, 0);
                    }
                    else if (n.Model.Category == ModelCategory.Dummy)
                    {
                        lines.Add($"{indent(lvl)}{n}");
                        n = GetNext(next, 0);
                    }
                    else
                    {
                        if (showConnections || !(n.Block is HlxConnector))
                            lines.Add($"{indent(lvl)}{n}");
                        n = GetNext(next, 0);
                    }
                }

            }
            lines.Add("");
            return lines;
        }
    }
}
