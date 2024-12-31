using HXSearch.Hlx;
using HXSearch.Models;
using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.Search;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace HXSearch
{
    // QuikGraph library https://github.com/KeRNeLith/QuikGraph/wiki/README


    internal class Preset
    {
        internal delegate void PreTraversalHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset);
        internal delegate void PreRootHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node root);
        internal delegate void PreLinearPathHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node root);
        internal delegate void SplitHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel);
        internal delegate void EndParallelSegmentHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel);
        internal delegate void JoinHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel);
        internal delegate void NodeHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel);
        internal delegate void PostLinearPathHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, List<Node> path);
        internal delegate void PostRootHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node root);
        internal delegate void PostTraversalHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset);

        public event PreTraversalHandler? OnPreTraversal;
        public event PreRootHandler? OnPreRoot;
        public event PreLinearPathHandler? OnPreLinearPath;
        public event SplitHandler? OnSplit;
        public event EndParallelSegmentHandler? OnEndParallelSegment;
        public event JoinHandler? OnJoin;
        public event NodeHandler? OnProcessNode;
        public event PostLinearPathHandler? OnPostLinearPath;
        public event PostRootHandler? OnPostRoot;
        public event PostTraversalHandler? OnPostTraversal;

        // When copying subgraphs to another graph, we create new Nodes in the
        // target graph. This structures maps serialNumber for Nodes in the
        // source graph to the correpsonding "replacement" node in the target
        // graph. So once we have created the replacement node, we use it when
        // creating subsequent edges
        private readonly Dictionary<int, Node> copiedNodesMap = new(100);

        public readonly string Name = "";
        public readonly string FQN = "";
        public readonly List<Dsp> Dsp = new(2); // populated gr the HlxDsp data in the Json file
        private readonly AdjacencyGraph<Node, Edge<Node>> presetGraph = new();
        public Preset(string fqn)
        {
            NodeFactory.Instance.Reset();
            FQN = fqn;
            HlxFile hlx = HlxFile.Load(FQN);
            if (null != hlx && null == hlx.data)
            {
                throw new InvalidDataException($"Preset is protected, not analyzed {FQN}");
            }
            else if (null != hlx && null != hlx.data)
            {
                Name = hlx.data.meta.name;
                for (int d = 0; d < hlx.data.tone.Dsp.Count; d++)
                    Dsp.Add(new Dsp(d, hlx.data.tone.global.Topology[d], hlx.data.tone.Dsp[d]));

                presetGraph = Dsp[0].DspGraph;

                // connect Dsp0 audio outs to Dsp1 audio ins as necessary
                ConnectDspGraphs(audioOutGraph: presetGraph, audioInGraph: Dsp[1].DspGraph);

                // if there are any signal chains in dsp1 that aren't connected dsp0 outputs, pull them into the preset-level graph
                ImportStandalonePaths(targetGraph: presetGraph, sourceGraph: Dsp[1].DspGraph);

                // if we have multiple chains from the same physical input, show
                // them as a single input which splits into multiple paths.
                HandleParallelInputs(presetGraph);

                // If we have signal paths existing to the same output port,
                // shows them as merging Must run AFTER HandleParallelInputs or
                // else we won't recognize "implied" parallelism that originates
                // as multiple paths from the same external input port.
                CloseOpenSplits(presetGraph);
            }
        }
        private void HandleParallelInputs(AdjacencyGraph<Node, Edge<Node>> gr)
        {
            bool needToCheckAgain;
            do
            {
                needToCheckAgain = false;
                // get root inputs coming from external inputs ports
                List<Node> rootInputs = [.. gr.Roots()
                                        .Where(
                                            n => n.Model.Category == ModelCategory.Input &&
                                            n.Block is HlxInput inputBlock &&
                                            0 != inputBlock.input )
                                        .OrderBy(n => ((HlxInput)n.Block).input)];
                while (rootInputs.Count >= 2)
                {
                    if (((HlxInput)rootInputs[0].Block).input == ((HlxInput)rootInputs[1].Block).input)
                    {
                        InsertSplitAfter([rootInputs[0], rootInputs[1]]);
                        rootInputs.RemoveRange(0, 2);
                        needToCheckAgain = true;
                    }
                    else
                    {
                        rootInputs.RemoveAt(0);
                    }
                }
            } while (needToCheckAgain);
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
                List<Node> leafs = [.. gr.Vertices
                                        .Where(
                                            n => null != n.Split &&
                                            (n.Model.Category == ModelCategory.Output || n.Model.Category == ModelCategory.Dummy)
                                            && 0 == gr.OutDegree(n))
                                        .OrderBy(n => n.Split?.SerialNumber)];

                while (null != leafs && leafs.Count >= 2)
                {
                    if (leafs[0].Split?.SerialNumber == leafs[1].Split?.SerialNumber && (leafs[0].Split?.OutputPort == leafs[1].Split?.OutputPort))
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
        private void InsertSplitAfter(Node[] nodes)
        {
            // add a solit to the graph, inserting it afer each Node in the
            // given array of nodes and those nodes downstream targets.

            List<Node> removeNodes = new(nodes.Length - 1);
            Node s = NodeFactory.Instance.NewNode(new HlxSplit() { model = ModelId.ImpliedSplit.ToString() });
            int nodeNum = 0;
            foreach (Node n in nodes)
            {
                List<Edge<Node>> originalOutEdges = [.. presetGraph.OutEdges(n)];

                // remove current downstream links from the node
                foreach (Edge<Node> e in originalOutEdges) presetGraph.RemoveEdge(e);

                presetGraph.AddVerticesAndEdge(new Edge<Node>(n, s)); // new split becomes the new downstream target for this node

                // add the original downstream nodes as S's downstream targets
                foreach (Edge<Node> e in originalOutEdges) presetGraph.AddVerticesAndEdge(new Edge<Node>(s, e.Target));

                // we will remove all but the first node from the graph
                if (nodeNum > 0) removeNodes.Add(n);

                nodeNum++;
            }

            foreach (Node obsoleteNode in removeNodes)
                presetGraph.RemoveVertex(obsoleteNode);

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
        }
        private void ImportStandalonePaths(AdjacencyGraph<Node, Edge<Node>> targetGraph, AdjacencyGraph<Node, Edge<Node>> sourceGraph)
        {
            foreach (Node n in sourceGraph.Roots())
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
                        ConnectOutputToInput(audioOutGraph: audioOutGraph, audioOutNode: audioOutNode, audioInGraph: audioInGraph, inputName: "HD2_AppDSPFlow2Input");
                        break;
                    case 4:

                        // The output routes to both dsp1 inputs. This is a
                        // "hidden" split because it introduces a parallel path
                        // in the aggregate signal chain. So we add a split
                        // before connecting to the dsp1 inputs
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
        private static Node? FirstTarget(AdjacencyGraph<Node, Edge<Node>> gr, Node? n) => null == n ? null : gr.OutEdges(n).FirstOrDefault()?.Target;
        private static void PushFirstTarget(Stack<Node?> stack, AdjacencyGraph<Node, Edge<Node>> gr, Node? n)
        {
            if (null == n) return;
            Edge<Node>? e = gr.OutEdges(n).FirstOrDefault();
            if (null != e) stack.Push(e.Target);
        }
        public void LinearPathsTraverse(IEnumerable<Node>? roots = null)
        {
            AdjacencyGraph<Node, Edge<Node>> graph = presetGraph;

            OnPreTraversal?.Invoke(graph, this);
            roots ??= graph.Roots().Where(n => n.Block is HlxInput inp && 0 != inp.input);

            foreach (Node rootInput in roots)
            {
                Stack<List<Node>> paths = new(10);
                OnPreRoot?.Invoke(graph, this, rootInput);
                paths.Push(new List<Node>([rootInput]));

                while (paths.Count > 0)
                {
                    List<Node> path = paths.Pop();
                    Node n = path.Last();
                    Node? next = FirstTarget(graph, n);

                    if (n.Model.Category == ModelCategory.Split)
                    {
                        OnSplit?.Invoke(graph, this, n, 0);
                        List<Edge<Node>> outEdges = [.. presetGraph.OutEdges(n).ToList()];
                        for (int i = 1; i < outEdges.Count; i++)
                            paths.Push(new List<Node>(path) { outEdges[i].Target }); // push a new list representing the current path plus the next target
                    }

                    OnProcessNode?.Invoke(graph, this, n, 0);

                    if (null == next)
                    {
                        OnPostLinearPath?.Invoke(graph, this, path);
                    }
                    else
                    {
                        path.Add(next);
                        paths.Push(path);
                    }
                }
                OnPostRoot?.Invoke(graph, this, rootInput);
            }
            OnPostTraversal?.Invoke(graph, this);
        }
        public void FullTraverse(IEnumerable<Node>? roots = null)
        {
            AdjacencyGraph<Node, Edge<Node>> graph = presetGraph;
            int lvl = 1;
            Stack<Node?> nextNode = new(); // stack of tuples: node to proc

            OnPreTraversal?.Invoke(graph, this);

            if (null == roots)
                // if they don't supply roots to start from we use the graph's
                // roots. If the graph is correctly constructed and the preset
                // is as expected these will be non-zero inputs, which are true
                // external inputs. But we filter defensively anyway.
                roots = graph.Roots().Where(n => n.Block is HlxInput inp && 0 != inp.input);

            foreach (Node rootInput in roots)
            {
                OnPreRoot?.Invoke(graph, this, rootInput);
                nextNode.Push(rootInput);
                while (nextNode.Count > 0)
                {
                    Node? n = nextNode.Pop();

                    if (n?.Block is HlxSplit split)
                    {
                        OnSplit?.Invoke(graph, this, n, lvl);
                        nextNode.Push(null); // this will mark the end of this split's outedges
                        List<Edge<Node>> outEdges = [.. presetGraph.OutEdges(n).ToList()];
                        for (int i = outEdges.Count - 1; i >= 0; i--)
                            nextNode.Push(outEdges[i].Target);
                        lvl++;
                    }
                    else if (n?.Block is HlxJoin)
                    {
                        if (0 == nextNode.Count)
                        {
                            // a join with no preceding split is a no-op, just keep going
                            PushFirstTarget(nextNode, presetGraph, n);
                        }
                        else if (null == nextNode.Peek())
                        {
                            // all of this join's splits have been traversed
                            nextNode.Pop(); // remove and discard the marker
                            lvl--;
                            OnJoin?.Invoke(graph, this, n, lvl);
                            PushFirstTarget(nextNode, presetGraph, n);
                        }
                        else
                        {
                            // nothing to push, the traversal will continue from next item on the stack
                            OnEndParallelSegment?.Invoke(graph, this, n, lvl);
                        }
                    }
                    else
                    {
                        if (null != n) OnProcessNode?.Invoke(graph, this, n, lvl);
                        PushFirstTarget(nextNode, presetGraph, n);
                    }
                }
                OnPostRoot?.Invoke(graph, this, rootInput);
            }
            OnPostTraversal?.Invoke(graph, this);
        }
    }
}