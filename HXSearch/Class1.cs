using HXSearch.Hlx;
using HXSearch.Models;
using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.Search;
namespace HXSearch
{
    public class Class1
    {
        public void Test()
        {
            var graph = new AdjacencyGraph<Node, Edge<Node>>();

            Node input = NodeFactory.Instance.NewNode(new HlxBlock() { model = ModelId.HD2_AppDSPFlow1Input.ToString() });
            Node distortion = NodeFactory.Instance.NewNode(new HlxBlock() { model = ModelId.HD2_DistMinotaur.ToString() });
            Node amp = NodeFactory.Instance.NewNode(new HlxBlock() { model = ModelId.HD2_AmpBritP75Nrm.ToString() });
            Node vol = NodeFactory.Instance.NewNode(new HlxBlock() { model = ModelId.HD2_VolPanVol.ToString() });
            Node split = NodeFactory.Instance.NewNode(new HlxBlock() { model = ModelId.HD2_AppDSPFlowSplitAB.ToString() });
            Node reverb = NodeFactory.Instance.NewNode(new HlxBlock() { model = ModelId.HD2_ReverbEcho.ToString() });
            Node delay = NodeFactory.Instance.NewNode(new HlxBlock() { model = ModelId.HD2_DelayModChorusEcho.ToString() });
            Node merge = NodeFactory.Instance.NewNode(new HlxBlock() { model = ModelId.HD2_AppDSPFlowJoin.ToString() });
            Node output = NodeFactory.Instance.NewNode(new HlxBlock() { model = ModelId.HD2_AppDSPFlowOutput.ToString() });
            Node filter = NodeFactory.Instance.NewNode(new HlxBlock() { model = ModelId.HD2_FilterAshevillePattrn.ToString() });

            graph.AddVerticesAndEdge(new Edge<Node>(input, distortion));
            graph.AddVerticesAndEdge(new Edge<Node>(distortion, amp));
            graph.AddVerticesAndEdge(new Edge<Node>(amp, vol));
            graph.AddVerticesAndEdge(new Edge<Node>(vol, split));
            graph.AddVerticesAndEdge(new Edge<Node>(split, reverb));
            graph.AddVerticesAndEdge(new Edge<Node>(split, filter));
            graph.AddVerticesAndEdge(new Edge<Node>(filter, merge));
            graph.AddVerticesAndEdge(new Edge<Node>(reverb, delay));
            graph.AddVerticesAndEdge(new Edge<Node>(delay, merge));
            graph.AddVerticesAndEdge(new Edge<Node>(merge, output));

            Console.WriteLine($"Leaf Nodes:");
            foreach (Node b in graph.Vertices.Where(b => 0 == graph.OutDegree(b)))
                Console.WriteLine($"    {b}");


            foreach (Node b in graph.Vertices)
            {
                Console.WriteLine($"{b.SerialNumber}:{b.Model.DisplayName}");
                foreach (var e in graph.OutEdges(b))
                    Console.WriteLine($"  -> {e.Target.SerialNumber}:{e.Target.Model.DisplayName}");
            }

            Console.WriteLine("Topological Sort");
            foreach (Node b in graph.TopologicalSort())
            {
                Console.WriteLine($"    {b}");
            }
            var dfs = new DepthFirstSearchAlgorithm<Node, Edge<Node>>(graph);
            dfs.SetRootVertex(input);
            dfs.ExamineEdge += Dfs_BackConnectSplits;
            dfs.Compute();
            dfs.ExamineEdge -= Dfs_BackConnectSplits;

            dfs.ExamineEdge += Dfs_Display;
            dfs.Compute();
            dfs.ExamineEdge -= Dfs_Display;
            Preset pre = new("E:/All/Documents/Line 6/Tones/Helix/Backup - Whole System/2.81 2019 08 12(2)/Setlist2-FACTORY 2/Preset083-Unicorn In A Box.hlx");
        }

        private void Dfs_Display(Edge<Node> edge)
        {
            Console.WriteLine($"DFS_Display {edge}");
        }

        private void Dfs_BackConnectSplits(Edge<Node> edge)
        {
            if (edge.Source.Model.Category == ModelCategory.Split)
                edge.Target.Split = edge.Source;
            else if (edge.Source.Model.Category == ModelCategory.Merge)
                edge.Target.Split = edge.Source.Split?.Split;
            else
                edge.Target.Split = edge.Source.Split;
            Console.WriteLine($"Dfs_BackConnectSplits {edge}");
        }
    }
}
