using System.Text;
using HXSearch.Hlx;
using HXSearch.Models;
using QuikGraph;


namespace HXSearch.TraversalHandlers
{
    internal class TrueSignature
    {
        private readonly StringBuilder sb = new(50);

        public string Signature => sb.ToString();

        internal void Subscribe(Preset preset)
        {
            preset.OnSplit += SplitHandler;
            preset.OnEndParallelSegment += EndParallelSegmentHandler;
            preset.OnJoin += JoinHandler;
            preset.OnProcessNode += NodeHandler;
        }
        internal void UnSubscribe(Preset preset)
        {
            preset.OnSplit -= SplitHandler;
            preset.OnEndParallelSegment -= EndParallelSegmentHandler;
            preset.OnJoin -= JoinHandler;
            preset.OnProcessNode -= NodeHandler;
        }

        internal void SplitHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel) => sb.Append('(');
        internal void EndParallelSegmentHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel) => sb.Append('|');
        internal void JoinHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel) => sb.Append(')');
        internal void NodeHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            if (null == n || null == n.Model || n.Model.Category == ModelCategory.Dummy) return;
            sb.Append(n.Model.Signature);
        }
    }
}
