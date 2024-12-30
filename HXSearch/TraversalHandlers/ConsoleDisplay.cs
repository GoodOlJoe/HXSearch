using HXSearch.Hlx;
using HXSearch.Models;
using QuikGraph;

namespace HXSearch.TraversalHandlers
{
    internal class ConsoleDisplay(bool showConnections)
    {
        private const int indentSize = 4;
        private const string indentStock = "                                                                                                                        ";

        private readonly List<string> _lines = new(50);
        private static string Indent(int level) => indentStock[0..(level * indentSize)];
        private readonly bool ShowConnections = showConnections;

        public List<string> OutputLines => _lines;

        internal void Subscribe(Preset preset)
        {
            preset.OnPreTraversal += PreTraversalHandler;
            preset.OnPreRoot += PreRootHandler;
            preset.OnSplit += SplitHandler;
            preset.OnEndParallelSegment += EndParallelSegmentHandler;
            preset.OnJoin += JoinHandler;
            preset.OnProcessNode += NodeHandler;
            //preset.OnPostRoot += PostRootHandler;
            //preset.OnPostTraversal += PostTraversalHandler;
        }
        internal void UnSubscribe(Preset preset)
        {
            preset.OnPreTraversal -= PreTraversalHandler;
            preset.OnPreRoot -= PreRootHandler;
            preset.OnSplit -= SplitHandler;
            preset.OnEndParallelSegment -= EndParallelSegmentHandler;
            preset.OnJoin -= JoinHandler;
            preset.OnProcessNode -= NodeHandler;
            //preset.OnPostRoot -= PostRootHandler;
            //preset.OnPostTraversal -= PostTraversalHandler;
        }

        internal void PreTraversalHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset)
        {
            _lines.AddRange([
                "",
                "",
                $"Preset display name: {preset.Name}",
                $"Preset file:         {preset.FQN}",
                $"Topology:            {preset.Dsp[0].Topology} {preset.Dsp[1].Topology}"
            ]);
        }
        internal void PreRootHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node root)
        {
            if (null != root && null != root.Block && root.Block is HlxInput inp)
            {
                _lines.Add("");
                _lines.Add($"=== dsp{inp.dspNum} input{inp.inputNum} ===============");
            }
        }
        internal void SplitHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            _lines.Add($"{Indent(splitLevel)}(");
        }
        internal void EndParallelSegmentHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            _lines.Add($"{Indent(splitLevel)}--- and ---");
        }
        internal void JoinHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            _lines.Add($"{Indent(splitLevel)})");
        }
        internal void NodeHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            if (n == null) return;
            bool showIt = true;
            switch (n.Model.Category)
            {
                case ModelCategory.Dummy:
                    showIt = false;
                    break;
                case ModelCategory.Input:
                case ModelCategory.Output:
                    showIt = ShowConnections;
                    break;
                default:
                    break;
            }
            if (showIt) _lines.Add($"{Indent(splitLevel)}{n}");
        }
        //internal void PostRootHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node root) { }
        //internal void PostTraversalHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset) { }
    }
}
