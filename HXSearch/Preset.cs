using HXSearch.Hlx;
using HXSearch.Models;
using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.Search;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Text;

namespace HXSearch
{
    // QuikGraph library https://github.com/KeRNeLith/QuikGraph/wiki/README

    internal class Preset
    {
        #region Delegates and Events
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
        #endregion Delegates and Events
        #region public Properties
        public readonly string Name = "";
        public readonly string FQN = "";
        public readonly List<Dsp> Dsp = new(2); // populated gr the HlxDsp data in the Json file
        #endregion public Properties
        #region private Properties
        private readonly AdjacencyGraph<Node, Edge<Node>> presetGraph = new();
        // When copying subgraphs to another graph, we create new Nodes in the
        // target graph. This structures maps serialNumber for Nodes in the
        // source graph to the correpsonding "replacement" node in the target
        // graph. So once we have created the replacement node, we use it when
        // creating subsequent edges
        private readonly Dictionary<int, Node> copiedNodesMap = new(100);
        #endregion private Properties
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

                // if there are any signal chains in dsp1 that aren't connected to dsp0 outputs, pull them into the preset-level graph
                ImportStandalonePaths(targetGraph: presetGraph, sourceGraph: Dsp[1].DspGraph);

                // if we have multiple chains from the same physical input, show
                // them as a single input which splits into multiple paths.
                HandleParallelInputs(presetGraph);

                // For certain combinations of topologies and routings we can
                // end up with an extraneous Join in the graph after dsp0
                // outputs are connected to dsp1 inputs. For example, if dsp1
                // has external inputs and an ABJ routing, and a dsp0 output
                // routes to either dsp1-A or -B (but not both) then the join in
                // dsp1 will have been duplicated into the overall graph, but
                // without a corresponding preceding split. Hard to picture
                // without a diagram, but basically that join in dsp1 is
                // meaningful only within the signal path originating from the
                // dsp1's external inputs. It's not meaningful when considering
                // dsp1 A or B in its additional role as a continuation from a
                // dsp0 output. But rather than trying to detect it during the
                // ConnectOutputToInput process, where you'd have to continually
                // repropagate the splits backpointers, we just let is get
                // included and then propagate splits once, and remove any joins
                // that don't have a corresponding split.
                RemoveExtraneousJoins(presetGraph);

                // If we have signal paths existing to the same output port,
                // shows them as merging Must run AFTER HandleParallelInputs or
                // else we won't recognize "implied" parallelism that originates
                // as multiple paths from the same external input port.
                CloseOpenSplits(presetGraph);
            }
        }
        #region Private methods
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
                                        .OrderBy(n => n.Block.DspNum)
                                        .ThenBy(n => ((HlxInput)n.Block).input)];
                while (rootInputs.Count >= 2)
                {
                    if (rootInputs.Count > 2 && rootInputs[0].Block.DspNum != rootInputs[1].Block.DspNum)
                    {
                        // there are at least 3 inputs to merge, and the first
                        // two are from different DSPs. When we have inputs to
                        // handle from different DSPs we want to handle any on
                        // the same DSP first. So drop this first one for now,
                        // letting the next two (if they are from the same DSP)
                        // get handled together first.
                        rootInputs.RemoveAt(0);
                    }
                    else if (((HlxInput)rootInputs[0].Block).input == ((HlxInput)rootInputs[1].Block).input)
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
                PropagateSplitsAndOutputPorts(gr);
                List<Node> leafs = [.. gr.Vertices
                                        .Where( n =>
                                            (n.Model.Category == ModelCategory.Output  || n.Model.Category == ModelCategory.Merge)
                                            && 0 == gr.OutDegree(n))
                                        .OrderBy(n => n.Split?.SerialNumber)
                                        .ThenBy(n => n.Depth)];

                while (null != leafs && leafs.Count >= 2)
                {
                    //if (leafs[0].Split?.SerialNumber == leafs[1].Split?.SerialNumber && (leafs[0].Split?.OutputPort == leafs[1].Split?.OutputPort))
                    //if (HaveCommonAncestor(leafs[0], leafs[1]) && (leafs[0].Split?.OutputPort == leafs[1].Split?.OutputPort))
                    //if (HaveCommonAncestor(leafs[0], leafs[1]) && (leafs[0].OutputPort == leafs[1].OutputPort))
                    if (leafs[0].OutputPort == leafs[1].OutputPort && leafs[0].Depth == leafs[1].Depth)
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
        //private static void Deepen(Node n, AdjacencyGraph<Node, Edge<Node>> gr)
        //{
        //    // increase by 1 the depth of the given Node. We do this by putting
        //    // an open (unjoined) split ahead of it

        //    List<Edge<Node>> originalInEdges = gr.Edges.Where(e => e.Target == n).ToList();
        //    //List<Edge<Node>> originalOutEdges = gr.Edges.Where(eIn => eIn.Source == n).ToList();

        //    Node s = NodeFactory.Instance.NewNode(new HlxSplit() { model = ModelId.ImpliedSplit.ToString() });
        //    //Node j = NodeFactory.Instance.NewNode(new HlxJoin() { model = ModelId.ImpliedJoin.ToString() });
        //    //gr.AddVerticesAndEdge(new Edge<Node>(s, j)); // one side of the split is "empty" -- connects straight to the join..
        //    gr.AddVerticesAndEdge(new Edge<Node>(s, n)); // ...the other side connects to the node we're deepening...
        //    //gr.AddVerticesAndEdge(new Edge<Node>(n, j)); // and then the node connects to the join

        //    // anything that was directly upstream of n becomes directly upstream of s
        //    foreach (Edge<Node> e in originalInEdges)
        //    {
        //        gr.AddVerticesAndEdge(new Edge<Node>(e.Source, s));
        //        gr.RemoveEdge(e);
        //    }
        //    // anything that was directly downstream of n becomes directly downstream of j
        //    //foreach (Edge<Node> eIn in originalOutEdges)
        //    //{
        //    //    gr.AddVerticesAndEdge(new Edge<Node>(j, eIn.Target));
        //    //    gr.RemoveEdge(eIn);
        //    //}
        //}
        private static bool HaveCommonAncestor(Node n1, Node n2)
        {
            return n1.Ancestors.Intersect(n2.Ancestors).Any();
        }
        private void InsertSplitAfter(Node[] nodes)
        {
            // add a split to the graph, inserting it afer each Node in the
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
            // Add a join to the graph, inserting it between each Node in the
            // given array of nodes and those nodes downstream targets.

            Node j = NodeFactory.Instance.NewNode(new HlxJoin() { model = ModelId.ImpliedJoin.ToString() });

            foreach (Node n in nodes)
            {
                List<Edge<Node>> originalOutEdges = [.. presetGraph.OutEdges(n)];

                // remove current downstream links from the node
                foreach (Edge<Node> e in originalOutEdges) presetGraph.RemoveEdge(e);

                presetGraph.AddVerticesAndEdge(new Edge<Node>(n, j)); // J becomes the new downstream target for this node

                // add the original downstream nodes as J's downstream targets
                foreach (Edge<Node> e in originalOutEdges) presetGraph.AddVerticesAndEdge(new Edge<Node>(j, e.Target));
            }
        }
        private void PropagateSplitsAndOutputPorts(AdjacencyGraph<Node, Edge<Node>> gr)
        {
            var dfs = new DepthFirstSearchAlgorithm<Node, Edge<Node>>(gr);
            dfs.ExamineEdge += Dfs_BackConnectSplits;
            dfs.ExamineEdge += Dfs_PropagateOutputPort;
            dfs.ExamineEdge += Dfs_CalculateDepth;
            dfs.Compute();
            dfs.ExamineEdge -= Dfs_BackConnectSplits;
            dfs.ExamineEdge -= Dfs_PropagateOutputPort;
            dfs.ExamineEdge -= Dfs_CalculateDepth;
        }
        private void Dfs_CalculateDepth(Edge<Node> edge)
        {
            if (-1 == edge.Source.Depth)
                edge.Source.Depth = 0;

            if (edge.Source.Model.Category == ModelCategory.Split)
                edge.Target.Depth = edge.Source.Depth + 1;

            else if (edge.Target.Model.Category == ModelCategory.Merge)
                edge.Target.Depth = edge.Source.Depth - 1;

            else
                edge.Target.Depth = edge.Source.Depth;
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
            if (edge.Target.Model.Category == ModelCategory.Merge)
            {
                // The TARGET is a join. Joins belong to the non-split Path of
                // the corresponding split. That is, they are not one of the
                // corresponding split's parallel Path, they are on the same
                // Path as the corresponding split. So we walk back until we
                // find our closest upstream split, then use that split's parent
                // split as our split.
                Node? sp = edge.Source;
                while (null != sp && sp.Model.Category != ModelCategory.Split)
                    sp = sp.Split;

                if (null == sp)
                    edge.Target.Split = null; // the join has no upstream split
                else
                    edge.Target.Split = sp.Split;
            }
            else if (edge.Source.Model.Category == ModelCategory.Split)
            {
                // The SOURCE is a split (and the target is not a join). The
                // target is on one of it's parent split's parallel paths.
                edge.Target.Split = edge.Source;
            }
            else
            {
                edge.Target.Split = edge.Source.Split;
            }
        }
        private void ImportStandalonePaths(AdjacencyGraph<Node, Edge<Node>> targetGraph, AdjacencyGraph<Node, Edge<Node>> sourceGraph)
        {
            copiedNodesMap.Clear(); // added 1

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
                copiedNodesMap.Clear();
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
                        // "hidden" split because it introduces a parallel Path
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
        private void RemoveExtraneousJoins(AdjacencyGraph<Node, Edge<Node>> gr)
        {
            PropagateSplitsAndOutputPorts(gr);
            HashSet<Node> extraJoins = new(2);

            // list of upstream edges to all extra joins...the edge's Source
            // nodes are the extra joins' upstream nodes, which must be
            // reconnected to the extra joins' downstream nodes
            List<Edge<Node>> extraJoinInEdges = [.. gr.Edges.Where(e => e.Target.Model.Category == ModelCategory.Merge && null == e.Target.Split)];

            foreach (Edge<Node> eIn in extraJoinInEdges)
            {
                // we'll delete any extra joins (and their connected edges)
                // after bypassing them
                extraJoins.Add(eIn.Target);

                foreach (Edge<Node> eOut in gr.OutEdges(eIn.Target))
                    gr.AddVerticesAndEdge(new Edge<Node>(eIn.Source, eOut.Target)); // bypass the join
            }

            foreach (Node n in extraJoins)
                gr.RemoveVertex(n);
        }
        private void ConnectOutputToInput(AdjacencyGraph<Node, Edge<Node>> audioOutGraph, Node audioOutNode, AdjacencyGraph<Node, Edge<Node>> audioInGraph, string inputName)
        {
            foreach (Node toInputNode in audioInGraph.Vertices.Where(n => n.Model.Category == ModelCategory.Input && n.Model.Name.Equals(inputName)))
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
        private void CopyToPresetGraphStartingAt(AdjacencyGraph<Node, Edge<Node>> sourceGraph, Node copyFromSourceRoot)
        {
            var dfs = new DepthFirstSearchAlgorithm<Node, Edge<Node>>(sourceGraph);
            dfs.ExamineEdge += Dfs_ProcessEdge;
            dfs.Compute(copyFromSourceRoot); // travers starting at the given source node
            dfs.ExamineEdge -= Dfs_ProcessEdge;
        }
        private void Dfs_ProcessEdge(Edge<Node> edge)
        {
            //Node target = edge.Target;

            if (!copiedNodesMap.ContainsKey(edge.Source.SerialNumber))
                copiedNodesMap.Add(edge.Source.SerialNumber, NodeFactory.Instance.NewNode(edge.Source.Block));

            //if (edge.Target.Model.Category == ModelCategory.Merge && copiedNodesMap.TryGetValue(edge.Target.SerialNumber, out Node? value))
            //{
            //    // special case for Joins: 
            //    target = value;
            //}
            if (!copiedNodesMap.ContainsKey(edge.Target.SerialNumber))
            {
                copiedNodesMap.Add(edge.Target.SerialNumber, NodeFactory.Instance.NewNode(edge.Target.Block));
                //target = copiedNodesMap[edge.Target.SerialNumber];
            }

            //Edge<Node> eIn = new Edge<Node>(copiedNodesMap[edge.Source.SerialNumber], copiedNodesMap[edge.Target.SerialNumber]);

            if (!presetGraph.Edges
                .Where(e =>
                    e.Source.SerialNumber == copiedNodesMap[edge.Source.SerialNumber].SerialNumber &&
                    e.Target.SerialNumber == copiedNodesMap[edge.Target.SerialNumber].SerialNumber).Any())
            {
                presetGraph.AddVerticesAndEdge(new Edge<Node>(copiedNodesMap[edge.Source.SerialNumber], copiedNodesMap[edge.Target.SerialNumber]));
            }
        }
        private static Node? FirstTarget(AdjacencyGraph<Node, Edge<Node>> gr, Node? n) => null == n ? null : gr.OutEdges(n).FirstOrDefault()?.Target;
        private static void PushFirstTarget(Stack<Node?> stack, AdjacencyGraph<Node, Edge<Node>> gr, Node? n)
        {
            if (null == n) return;
            Edge<Node>? e = gr.OutEdges(n).FirstOrDefault();
            if (null != e) stack.Push(e.Target);
        }
        #endregion Private methods
        #region Public interface methods
        public List<string> GraphToStrings()
        {
            // show all graph contents
            List<string> lines = new(presetGraph.EdgeCount);
            foreach (var v in presetGraph.Roots<Node, Edge<Node>>()) lines.Add($"Root {v}");
            foreach (Edge<Node> e in presetGraph.Edges) lines.Add(e.ToString());
            return lines;
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
                            paths.Push(new List<Node>(path) { outEdges[i].Target }); // push a new list representing the current Path plus the next target
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
        #endregion Public interface methods
    }
}