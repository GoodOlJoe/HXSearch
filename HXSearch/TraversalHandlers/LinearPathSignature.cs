using System.Text;
using HXSearch.Models;
using QuikGraph;


namespace HXSearch.TraversalHandlers
{
    internal class LinearPathSignature
    {
        internal IEnumerable<string> Paths => _paths;
        private readonly StringBuilder sb = new(50);
        private readonly List<string> _paths = new(10);

        internal void Subscribe(Preset preset) => preset.OnPostLinearPath += PostLinearPathHandler;
        internal void UnSubscribe(Preset preset) => preset.OnPostLinearPath -= PostLinearPathHandler;

        internal void PostLinearPathHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, List<Node> path)
        {
            sb.Clear();
            foreach (Node n in path.Where(n =>
                null != n.Model &&
                n.Model.Category != ModelCategory.Split &&
                n.Model.Category != ModelCategory.Merge &&
                n.Model.Category != ModelCategory.Dummy))
            {
                sb.Append(n.Model.Signature);
            }
            _paths.Add(sb.ToString());
        }
    }
}
