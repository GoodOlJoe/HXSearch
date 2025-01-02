using HXSearch.Hlx;
using HXSearch.Models;
using QuikGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using static HXSearch.Preset;

namespace HXSearch.TraversalHandlers
{
    internal class ParallelismSignature
    {
        private class TraversalInfo
        {
            private static char[] seps = ['.', '-'];
            public readonly Node Node;
            public readonly int SegmentSequence;
            public readonly string Path;
            public readonly string ParentPath;
            public readonly string TraversalId;
            public int PathLevelCount => PathLevels.Length;
            private readonly string[] PathLevels;
            public readonly string Ancestry;

            public TraversalInfo(Node n)
            {
                Node = n;
                TraversalId = n.TraversalId;
                string[] elements = n.TraversalId.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                SegmentSequence = int.Parse(elements.Last());
                PathLevels = elements[0..^1]; // all but the last
                Path = string.Join('.', PathLevels);
                ParentPath = string.Join('.', PathLevels[..^1]);

                Ancestry = "";
                Node? sp = n.Split;
                while (null != sp)
                {
                    Ancestry = $"{sp.TraversalId}:{Ancestry}";
                    sp = sp.Split;
                }
                Ancestry = $"R:{Ancestry}";
            }
            public override string ToString() => $"{Path}-{SegmentSequence}";
        }
        private Dictionary<Node, TraversalInfo> TiByNode = new(50);
        private Dictionary<string, Node> NodeByTraversalID = new(50);

        private List<TraversalInfo> AllTIs = new List<TraversalInfo>(50);
        internal IEnumerable<string> Paths => _paraChains;
        private readonly List<string> _paraChains = new(10);

        internal void Subscribe(Preset preset)
        {
            preset.OnPreTraversal += PreTraversalHandler;
            preset.OnPreRoot += PreRootHandler;
            preset.OnSplit += SplitHandler;
            preset.OnEndParallelSegment += EndParallelSegmentHandler;
            preset.OnJoin += JoinHandler;
            preset.OnProcessNode += NodeHandler;
            preset.OnPostRoot += PostRootHandler;
            preset.OnPostTraversal += PostTraversalHandler;
        }
        internal void UnSubscribe(Preset preset)
        {
            preset.OnPreTraversal -= PreTraversalHandler;
            preset.OnPreRoot -= PreRootHandler;
            preset.OnSplit -= SplitHandler;
            preset.OnEndParallelSegment -= EndParallelSegmentHandler;
            preset.OnJoin -= JoinHandler;
            preset.OnProcessNode -= NodeHandler;
            preset.OnPostRoot -= PostRootHandler;
            preset.OnPostTraversal -= PostTraversalHandler;
        }
        internal void PreTraversalHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset)
        {
        }
        internal void PreRootHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node root)
        {
            AllTIs.Clear();
        }
        internal void SplitHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            NodeHandler(graph, preset, n, splitLevel);
        }
        internal void EndParallelSegmentHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
        }
        internal void JoinHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            NodeHandler(graph, preset, n, splitLevel);
        }
        internal void NodeHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            if (string.IsNullOrEmpty(n.TraversalId)) return;
            TraversalInfo ti = new TraversalInfo(n);
            AllTIs.Add(ti);
            TiByNode.Add(n, ti);
            NodeByTraversalID.Add(ti.TraversalId, n);
        }
        internal void PostRootHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node root)
        {
            List<string> allSegments = new(10);
            foreach (TraversalInfo ti in AllTIs.OrderBy(ti => ti.Path))
            {
                if (!allSegments.Contains(ti.Path))
                {
                    allSegments.Add(ti.Path);
                    BuildOneParaChain(ti.Path);
                }
            }
        }
        internal void PostTraversalHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset)
        {
        }
        private bool IsPredecessor(TraversalInfo thisTi, TraversalInfo testPredTi)
        {
            // true if the given node is a predecessor to THIS 

            bool isPredecessor;

            // see if it's before me, on my same segment
            isPredecessor =
                testPredTi.Path.Equals(thisTi.Path) &&
                testPredTi.SegmentSequence < thisTi.SegmentSequence;

            // walk parent splits, see if it's before any of them
            if (!isPredecessor && null != thisTi.Node.Split)
            {
                isPredecessor = IsPredecessor(TiByNode[thisTi.Node.Split], testPredTi);
            }
            return isPredecessor;
        }
        private void BuildOneParaChain(string segmentPath)
        {

            // A is the actual modules on this parallel segment of a split, in
            // order
            List<TraversalInfo> A = AllTIs.Where(ti => ti.Path.Equals(segmentPath)).OrderBy(ti => ti.TraversalId).ToList();
            TraversalInfo tiFirst = A[0];
            if (null == tiFirst.Node.Split) return;


            // B is the list of modules effectively parallel to the given segment
            // This is modules truly parallel (the other side of the same split our segment came from)
            List<TraversalInfo> B = new(20);
            B.AddRange(AllTIs.Where(ti =>
                    ti.Ancestry.Equals(tiFirst.Ancestry) &&
                    !ti.Path.Equals(tiFirst.Path)).ToList());


            B.AddRange(AllTIs.Where(ti =>
                !B.Contains(ti) &&
                !tiFirst.Path.StartsWith(ti.Path) &&
                !ti.Path.StartsWith(tiFirst.Path)));

            // find my split (the split of tiFirst)
            // S = empty
            // J = empty
            // walk up the splits SP at a time
                // add everything on SP's path but sequence <= SP to the front of S
                // add everything on SP's path but sequence > SP to the end of J
            // connect S, a split, A|B, a join, J

            // walk up the joins JN at a time, adding everything 
            //List<TraversalInfo> S = AllTIs.Where(ti => IsPredecessor(TiByNode[MySplit], ti)).OrderBy(ti => ti.TraversalId).ToList();

        }
    }
}
