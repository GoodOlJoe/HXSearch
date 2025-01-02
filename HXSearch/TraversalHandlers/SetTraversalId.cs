using QuikGraph;

namespace HXSearch.TraversalHandlers
{
    internal class SetTraversalId
    {
        private readonly Dictionary<string, int> NextSequence = new(50);
        private readonly Dictionary<string, int> NextBranch = new(50);
        readonly Stack<string> pathStack = new(10);

        internal void Subscribe(Preset preset)
        {
            preset.OnPreRoot += PreRootHandler;
            preset.OnSplit += SplitHandler;
            preset.OnEndParallelSegment += EndParallelSegmentHandler;
            preset.OnJoin += JoinHandler;
            preset.OnProcessNode += NodeHandler;
        }
        internal void UnSubscribe(Preset preset)
        {
            preset.OnPreRoot -= PreRootHandler;
            preset.OnSplit -= SplitHandler;
            preset.OnEndParallelSegment -= EndParallelSegmentHandler;
            preset.OnJoin -= JoinHandler;
            preset.OnProcessNode -= NodeHandler;
        }
        internal void PreRootHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node root)
        {
            NextSequence.Clear(); NextSequence.Add("0", 0);
            NextBranch.Clear(); NextBranch.Add("0", 0);
            pathStack.Clear(); pathStack.Push(".0");
        }
        internal void SplitHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            ProcessNode(n);

            string curr = pathStack.Peek();
            int nextBranch = GetNextBranch(curr);
            pathStack.Push($"{curr}.{nextBranch}");
        }
        internal void EndParallelSegmentHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            _ = pathStack.Pop();
            string curr = pathStack.Peek();
            int nextBranch = GetNextBranch(curr);
            pathStack.Push($"{curr}.{nextBranch}");
        }
        internal void JoinHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            _ = pathStack.Pop();
            ProcessNode(n);
        }
        internal void NodeHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            ProcessNode(n);
        }
        private void ProcessNode(Node n)
        {
            n.Segment = pathStack.Peek();
            n.SegmentSequence = GetNextSequence(n.Segment);
        }
        private int GetNextSequence(string path)
        {
            NextSequence.TryAdd(path, 0);
            NextSequence[path]++;
            return NextSequence[path] - 1;
        }
        private int GetNextBranch(string path)
        {
            NextBranch.TryAdd(path, 0);
            NextBranch[path]++;
            return NextBranch[path] - 1;
        }
    }
}
