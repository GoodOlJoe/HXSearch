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
                    !ti.Path.Equals(tiFirst.Path))
                    .OrderBy(ti => ti.TraversalId).ToList());


            B.AddRange(AllTIs.Where(ti =>
                !B.Contains(ti) &&
                !tiFirst.Path.StartsWith(ti.Path) &&
                !ti.Path.StartsWith(tiFirst.Path)));

            TraversalInfo? tiSplit = TiByNode[tiFirst.Node.Split]; // the split preceding this segment
            List<TraversalInfo> S = new(10);
            List<TraversalInfo> J = new(10);
            // walk up the splits
            while (null != tiSplit)
            {

                // add nodes preceding this segment (everything on SP's path but sequence <= SP)
                S.AddRange(AllTIs.Where(ti =>
                    ti.Path.Equals(tiSplit.Path) && ti.SegmentSequence <= tiSplit.SegmentSequence)
                    .OrderBy(ti => ti.TraversalId).ToList());

                // Add nodes trailing this segment (everything on SP's path but sequence > SP)
                J.AddRange(AllTIs.Where(ti =>
                    ti.Path.Equals(tiSplit.Path) && ti.SegmentSequence > tiSplit.SegmentSequence)
                    .OrderBy(ti => ti.TraversalId).ToList());

                if (null != tiSplit.Node.Split)
                    tiSplit = TiByNode[tiSplit.Node.Split];
                else
                    tiSplit = null;
            }

            //// remove all splits 
            //for (int i = S.Count - 1; i >= 0; i--)
            //    if (S[i].Node.Model.Category == ModelCategory.Split)
            //        S.RemoveAt(i);

            //// remove all joins 
            //for (int i = J.Count - 1; i >= 0; i--)
            //    if (J[i].Node.Model.Category == ModelCategory.Merge)
            //        J.RemoveAt(i);

            _paraChains.Add(ParaChainSignature(S, A, B, J));
        }
        private static void StripNodes(List<TraversalInfo> list, List<ModelCategory> categories)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (categories.Contains(list[i].Node.Model.Category))
                    list.RemoveAt(i);
            }
        }
        private static string ParaChainSignature(
            List<TraversalInfo> S,
            List<TraversalInfo> A,
            List<TraversalInfo> B,
            List<TraversalInfo> J
            )
        {
            StringBuilder sb = new(50);
            if (S.Count > 0)
            {
                sb.Append(OneSegment(S));
                sb.Append("(");
            }
            if (A.Count > 0)
            {
                sb.Append(OneSegment(A));
                sb.Append("|");
            }
            if (B.Count > 0)
            {
                sb.Append(OneSegment(B));
                sb.Append(")");
            }
            if (J.Count > 0)
            {
                sb.Append(OneSegment(J));
            }
            return sb.ToString();
        }
        private static string OneSegment(List<TraversalInfo> list)
        {
            StripNodes(list, [ModelCategory.Split, ModelCategory.Merge]);

            if (list.Count == 0) return "";

            StringBuilder sb = new(50);
            foreach (var ti in list)
            {
                sb.Append($"{ti.Node.Model.Signature} ");
            }
            return sb.ToString();
        }
    }
}
