using HXSearch.Hlx;
using QuikGraph;
using QuikGraph.Collections;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.Search;
using System.Diagnostics.Tracing;
using HXSearch.Models;

namespace HXSearch
{
    internal class Dsp
    {
        public readonly int DspNum;
        public readonly string Topology;
        public readonly AdjacencyGraph<Node, Edge<Node>> DspGraph;

        public Dsp(int dspNum, string topology, HlxDsp hlxDsp)
        {
            if (null == hlxDsp) return;

            Topology = topology;
            DspNum = dspNum;
            DspGraph = BuildDspGraphByTopology(topology, hlxDsp);
            hlxDsp.Inputs[0].dspNum = dspNum;
            hlxDsp.Inputs[1].dspNum = dspNum;
            hlxDsp.Outputs[0].dspNum = dspNum;
            hlxDsp.Outputs[1].dspNum = dspNum;
        }
        private static AdjacencyGraph<Node, Edge<Node>> BuildDspGraphByTopology(string topology, HlxDsp hlxDsp)
        {
            var graph = new AdjacencyGraph<Node, Edge<Node>>();
            List<HlxBlock> blocks = hlxDsp.Blocks.OrderBy(b => b.position).ToList();

            Node input;
            Node output;
            Node split;
            Node join;
            switch (topology)
            {
                case "A":
                    input = NodeFactory.Instance.NewNode(hlxDsp.Inputs[0]);
                    output = NodeFactory.Instance.NewNode(hlxDsp.Outputs[0]);
                    AddStraightPathBetweenNodes(graph, hlxDsp, input, blocks, output);
                    break;

                case "AB":
                    foreach (int path in (int[])[0, 1])
                    {
                        input = NodeFactory.Instance.NewNode(hlxDsp.Inputs[path]);
                        output = NodeFactory.Instance.NewNode(hlxDsp.Outputs[path]);
                        AddStraightPathBetweenNodes(graph, hlxDsp, input, blocks.Where(b => path == b.path), output);
                    }
                    break;

                case "SABJ":
                    input = NodeFactory.Instance.NewNode(hlxDsp.Inputs[0]);
                    output = NodeFactory.Instance.NewNode(hlxDsp.Outputs[0]);
                    split = NodeFactory.Instance.NewNode(hlxDsp.Split);
                    join = NodeFactory.Instance.NewNode(hlxDsp.Join);

                    AddStraightPathBetweenNodes(graph, hlxDsp, input, blocks.Where(b => 0 == b.path && b.position < hlxDsp.Split.position), split);
                    AddStraightPathBetweenNodes(graph, hlxDsp, join, blocks.Where(b => 0 == b.path && b.position >= hlxDsp.Join.position), output);
                    AddStraightPathBetweenNodes(graph, hlxDsp, split, blocks.Where(b => 0 == b.path && b.position >= hlxDsp.Split.position && b.position < hlxDsp.Join.position), join);
                    AddStraightPathBetweenNodes(graph, hlxDsp, split, blocks.Where(b => 1 == b.path), join);
                    break;

                case "SAB":
                    input = NodeFactory.Instance.NewNode(hlxDsp.Inputs[0]);
                    split = NodeFactory.Instance.NewNode(hlxDsp.Split);
                    Node output0 = NodeFactory.Instance.NewNode(hlxDsp.Outputs[0]);
                    Node output1 = NodeFactory.Instance.NewNode(hlxDsp.Outputs[1]);

                    AddStraightPathBetweenNodes(graph, hlxDsp, input, blocks.Where(b => 0 == b.path && b.position < hlxDsp.Split.position), split);
                    AddStraightPathBetweenNodes(graph, hlxDsp, split, blocks.Where(b => 0 == b.path && b.position >= hlxDsp.Split.position), output0);
                    AddStraightPathBetweenNodes(graph, hlxDsp, split, blocks.Where(b => 1 == b.path), output1);
                    break;

                case "ABJ":
                    Node input0 = NodeFactory.Instance.NewNode(hlxDsp.Inputs[0]);
                    Node input1 = NodeFactory.Instance.NewNode(hlxDsp.Inputs[1]);
                    output = NodeFactory.Instance.NewNode(hlxDsp.Outputs[0]);
                    join = NodeFactory.Instance.NewNode(hlxDsp.Join);

                    AddStraightPathBetweenNodes(graph, hlxDsp, input0, blocks.Where(b => 0 == b.path && b.position < hlxDsp.Join.position), join);
                    AddStraightPathBetweenNodes(graph, hlxDsp, input1, blocks.Where(b => 1 == b.path), join);
                    AddStraightPathBetweenNodes(graph, hlxDsp, join, blocks.Where(b => 0 == b.path && b.position >= hlxDsp.Join.position), output);
                    break;
            }
            return graph;
        }
        private static void AddStraightPathBetweenNodes(AdjacencyGraph<Node, Edge<Node>> graph, HlxDsp hlxDsp, Node? head, IEnumerable<HlxBlock>? blocks, Node? tail)
        {
            if (null == head && null == blocks) return; // all we have is a tail, nothing to do
            if (null == tail && null == blocks) return; // all we have is a head, nothing to do

            Node? source = head;

            if (null != blocks)
                foreach (HlxBlock blk in blocks)
                {
                    if (null != source && null != tail)
                    {
                        Node target = NodeFactory.Instance.NewNode(blk);

                        if (target.Model.Category == ModelCategory.Amp && !string.IsNullOrEmpty(blk.cab))
                        {
                            // special case for Amp+Cab -- insert the amp and a new block representing the cab
                            graph.AddVerticesAndEdge(new Edge<Node>(source, target));
                            Node cab = GetImpliedCabNode(blk, hlxDsp);
                            graph.AddVerticesAndEdge(new Edge<Node>(target,cab));
                            target = cab;
                        }
                        else if (target.Model.Category == ModelCategory.DualCab)
                        {
                            // special case for Dual Cabs -- insert a mini graph
                            // representing a split/two parallel cabs/merge
                            (Node s, Node a, Node b, Node j) dcNodes = GetDualCabNodes(target.Block, hlxDsp);
                            graph.AddVerticesAndEdge(new Edge<Node>(source, dcNodes.s));
                            graph.AddVerticesAndEdge(new Edge<Node>(dcNodes.s, dcNodes.a));
                            graph.AddVerticesAndEdge(new Edge<Node>(dcNodes.s, dcNodes.b));
                            graph.AddVerticesAndEdge(new Edge<Node>(dcNodes.a, dcNodes.j));
                            graph.AddVerticesAndEdge(new Edge<Node>(dcNodes.b, dcNodes.j));
                            target = dcNodes.j;
                        }
                        else
                        {
                            graph.AddVerticesAndEdge(new Edge<Node>(source, target));
                        }
                        source = target;
                    }
                }

            if (null != source && null != tail)
                graph.AddVerticesAndEdge(new Edge<Node>(source, tail));
        }
        private static Node GetImpliedCabNode(HlxBlock blk, HlxDsp hlxDsp)
        {
            return blk.cab switch
            {
                "cab0" => NodeFactory.Instance.NewNode(new HlxCab() { model = hlxDsp.Cabs[0].model }),
                "cab1" => NodeFactory.Instance.NewNode(new HlxCab() { model = hlxDsp.Cabs[1].model }),
                "cab2" => NodeFactory.Instance.NewNode(new HlxCab() { model = hlxDsp.Cabs[2].model }),
                "cab3" => NodeFactory.Instance.NewNode(new HlxCab() { model = hlxDsp.Cabs[3].model }),
                _ => NodeFactory.Instance.NewNode(blk) // failsafe, should never happen
            };
        }
        private static (Node s, Node a, Node b, Node j) GetDualCabNodes(HlxBlock dualCabBlock, HlxDsp hlxDsp)
        {
            Node s = NodeFactory.Instance.NewNode(new HlxSplit() { model = ModelId.ImpliedDualCabSplit.ToString() });
            Node a = NodeFactory.Instance.NewNode(dualCabBlock);
            Node b = GetImpliedCabNode(dualCabBlock, hlxDsp);
            Node j = NodeFactory.Instance.NewNode(new HlxJoin() { model = ModelId.ImpliedJoin.ToString() });
            return (s, a, b, j);
        }
        public List<string> GraphToStrings()
        {
            List<string> lines = new(DspGraph.EdgeCount);
            foreach (var v in DspGraph.Roots<Node, Edge<Node>>()) lines.Add($"Root {v}");
            foreach (Edge<Node> e in DspGraph.Edges) lines.Add(e.ToString());
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

            List<string> lines = new(DspGraph.EdgeCount * 2)
            {
                "",
                "",
                $"DSP:      {DspNum}",
                $"Topology: {Topology}"
            };

            // If the graph is correctly constructed and the preset is as
            // expected, the only roots will be non-zero inputs, which are true
            // external inputs. But we defensively filter the roots with these
            // conditions anyway
            foreach (Node rootInput in DspGraph.Roots().Where(n => n.Block is HlxInput inp && 0 != inp.input))
            {
                HlxInput? inp = rootInput.Block as HlxInput;
                lines.Add("");
                lines.Add($"=== dsp{inp?.dspNum} input{inp?.inputNum} ===============");
                lines.Add("");

                Node? n = rootInput;

                Node? pathBHead = null;
                while (null != n)
                {
                    List<Edge<Node>> next = [.. DspGraph.OutEdges(n).ToList()];

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
