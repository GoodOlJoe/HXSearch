using HXSearch.Hlx;
using QuikGraph;
using QuikGraph.Collections;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.Search;
using System.Diagnostics.Tracing;

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
            DspGraph = BuildDspGraph(dspNum, topology, hlxDsp);
        }
        private static AdjacencyGraph<Node, Edge<Node>> BuildDspGraph(int dspNum, string topology, HlxDsp hlxDsp)
        {
            var graph = new AdjacencyGraph<Node, Edge<Node>>();

            AddInputToGraph(graph, dspNum, topology, inputNum: 0, topology.Contains('S'), topology.Contains('J'), hlxDsp);

            if (topology.StartsWith("AB")) // only AB and ABJ have separate chains starting on the second input
                AddInputToGraph(graph, dspNum, topology, inputNum: 1, hasSplit: false, hasJoin: false, hlxDsp);

            return graph;
        }
        private static void AddStraightPathBetweenNodes(AdjacencyGraph<Node, Edge<Node>> graph, Node? head, List<HlxBlock>? blocks, Node? tail)
        {
            if (null == blocks) return;
            Node? source = head;
            foreach (HlxBlock blk in blocks)
            {
                Node target = NodeFactory.Instance.NewNode(blk);
                if (null != source && null != tail)
                    graph.AddVerticesAndEdge(new Edge<Node>(source, target));
                source = target;
            }
            if (null != source && null != tail)
                graph.AddVerticesAndEdge(new Edge<Node>(source, tail));
        }
        private static void AddInputToGraph(AdjacencyGraph<Node, Edge<Node>> graph, int dspNum, string topology, int inputNum, bool hasSplit, bool hasJoin, HlxDsp hlxDsp)
        {
            // these inits can be confusing, but we're just data-proofing again
            // the hlx file not having a split or join block in the dsp. The
            // hasSplit/hasJoin values are set by the caller based on dsp
            // topology. But these blocks WILL be present in a device supporting
            // two paths on one input. But remember the split/join being present
            // doesn't mean the topology actually uses a split or join.
            int splitPos = 0;
            if (null == hlxDsp.Split) hasSplit = false;
            else splitPos = hlxDsp.Split.position;

            int joinPos = 0;
            if (null == hlxDsp.Join) hasJoin = false;
            else joinPos = hlxDsp.Join.position;

            // start with a list of all blocks in this DSP. This just makes the
            // algorithm easier to follow because I can remove blocks from this
            // last as I add them to the graph, so fewer limiting conditions.
            // Also I can order by position once and not have to do it each time
            // when I move blocks to the graph
            List<HlxBlock> allBlocks = new(hlxDsp.Blocks.Count);
            foreach (HlxBlock blk in hlxDsp.Blocks.OrderBy(b => b.position))
                allBlocks.Add(blk);

            Node input = NodeFactory.Instance.NewNode(hlxDsp.Inputs[inputNum]);
            Node source = input; // where to attach whatever block is next

            // S segment: add blocks up to and including the split
            Node? split;
            if (hasSplit && null != hlxDsp.Split)
            {
                List<HlxBlock> usedBlocks = new(hlxDsp.Blocks.Count);
                foreach (HlxBlock blk in allBlocks.Where(b => 0 == b.path && b.position < splitPos))
                {
                    Node target = NodeFactory.Instance.NewNode(blk);
                    usedBlocks.Add(blk);
                    graph.AddVerticesAndEdge(new Edge<Node>(source, target));
                    source = target;
                }
                split = NodeFactory.Instance.NewNode(hlxDsp.Split);
                graph.AddVerticesAndEdge(new Edge<Node>(source, split));
                foreach (HlxBlock blk in usedBlocks) allBlocks.Remove(blk);
            }

            // J segment: add the join and blocks after it
            Node? join;
            if (hasJoin && null != hlxDsp.Join)
            {
                join = NodeFactory.Instance.NewNode(hlxDsp.Join);
                source = join;
                List<HlxBlock> usedBlocks = new(hlxDsp.Blocks.Count);
                foreach (HlxBlock blk in allBlocks.Where(b => 0 == b.path && b.position >= joinPos))
                {
                    Node target = NodeFactory.Instance.NewNode(blk);
                    usedBlocks.Add(blk);
                    graph.AddVerticesAndEdge(new Edge<Node>(source, target));
                    source = target;
                }
                foreach (HlxBlock blk in usedBlocks) allBlocks.Remove(blk);
            }

            // Anything remaining on allBlocks is on the A (path 0) or B (path
            // 1) segments. How they interconnect depends on the topology

            // these will actually already have their correct value if we use
            // them below, but the compiler's static analysis thinks they might
            // be uninitialized if I don't do this here
            split = graph.Vertices.Where(n => n.Model.Category == Models.ModelCategory.Split).FirstOrDefault();
            join = graph.Vertices.Where(n => n.Model.Category == Models.ModelCategory.Merge).FirstOrDefault();

            switch (topology)
            {
                case "A":
                case "AB":
                    Node output = NodeFactory.Instance.NewNode(hlxDsp.Outputs[inputNum]);
                    AddStraightPathBetweenNodes(graph, input, allBlocks.Where(blk => inputNum == blk.path).ToList(), output);
                    break;

                case "SAB":
                    for (int path = 0; path <= 1; path++)
                    {
                        output = NodeFactory.Instance.NewNode(hlxDsp.Outputs[path]);
                        AddStraightPathBetweenNodes(graph, split, allBlocks.Where(blk => path == blk.path).ToList(), output);
                    }
                    break;

                case "SABJ":
                    for (int path = 0; path <= 1; path++)
                    {
                        output = NodeFactory.Instance.NewNode(hlxDsp.Outputs[path]);
                        AddStraightPathBetweenNodes(graph, split, allBlocks.Where(blk => path == blk.path).ToList(), join);
                    }
                    break;

                default:
                    break;

            }

        }
        public List<string> GraphToStrings()
        {
            List<string> lines = new(DspGraph.EdgeCount);
            foreach (var v in DspGraph.Roots<Node, Edge<Node>>()) lines.Add($"Root {v}");
            foreach (Edge<Node> e in DspGraph.Edges) lines.Add(e.ToString());
            return lines;
        }
    }
}
