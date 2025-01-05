using HXSearch.Hlx;
using QuikGraph;
using QuikGraph.Algorithms;
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
            if (null == hlxDsp)
            {
                // satisfy non-nullability and return
                Topology = "";
                DspGraph = new();
                return;
            }

            Topology = topology;
            DspNum = dspNum;
            foreach (HlxBlock b in hlxDsp.Blocks) b.DspNum = dspNum;
            DspGraph = BuildDspGraphByTopology(topology, hlxDsp);
            hlxDsp.Inputs[0].DspNum = dspNum;
            hlxDsp.Inputs[1].DspNum = dspNum;
            hlxDsp.Outputs[0].DspNum = dspNum;
            hlxDsp.Outputs[1].DspNum = dspNum;
        }
        private static AdjacencyGraph<Node, Edge<Node>> BuildDspGraphByTopology(string topology, HlxDsp hlxDsp)
        {
            var graph = new AdjacencyGraph<Node, Edge<Node>>();
            List<HlxBlock> blocks = [.. hlxDsp.Blocks.OrderBy(b => b.position)];

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
                            graph.AddVerticesAndEdge(new Edge<Node>(target, cab));
                            target = cab;
                        }
                        else if (target.Model.Category == ModelCategory.DualCab)
                        {
                            // special case for Dual Cabs -- insert a mini graph
                            // representing a split/two parallel cabs/merge
                            (Node s, Node a, Node b, Node j) = GetDualCabNodes(target.Block, hlxDsp);
                            graph.AddVerticesAndEdge(new Edge<Node>(source, s));
                            graph.AddVerticesAndEdge(new Edge<Node>(s, a));
                            graph.AddVerticesAndEdge(new Edge<Node>(s, b));
                            graph.AddVerticesAndEdge(new Edge<Node>(a, j));
                            graph.AddVerticesAndEdge(new Edge<Node>(b, j));
                            target = j;
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
    }
}
