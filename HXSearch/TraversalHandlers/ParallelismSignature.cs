using HXSearch.Models;
using QuikGraph;
using System.Text;

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
            public readonly string AncestryOfSplits;
            public int Column = -1; // the left/right position in a visual layout

            public TraversalInfo(Node n)
            {
                Node = n;
                TraversalId = n.TraversalId;
                string[] elements = n.TraversalId.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                SegmentSequence = int.Parse(elements.Last());
                PathLevels = elements[0..^1]; // all but the last
                Path = string.Join('.', PathLevels);
                ParentPath = string.Join('.', PathLevels[..^1]);

                AncestryOfSplits = "";
                Node? sp = n.Split;
                while (null != sp)
                {
                    AncestryOfSplits = $"{sp.TraversalId}:{AncestryOfSplits}";
                    sp = sp.Split;
                }
                AncestryOfSplits = $"R:{AncestryOfSplits}";
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
            //preset.OnSplit += SplitHandler;
            //preset.OnEndParallelSegment += EndParallelSegmentHandler;
            //preset.OnJoin += JoinHandler;
            //preset.OnProcessNode += NodeHandler;
            preset.OnProcessEdge += EdgeHandler;
            preset.OnPostRoot += PostRootHandler;
            preset.OnPostTraversal += PostTraversalHandler;
        }
        internal void UnSubscribe(Preset preset)
        {
            preset.OnPreTraversal -= PreTraversalHandler;
            preset.OnPreRoot -= PreRootHandler;
            //preset.OnSplit -= SplitHandler;
            //preset.OnEndParallelSegment -= EndParallelSegmentHandler;
            //preset.OnJoin -= JoinHandler;
            //preset.OnProcessNode -= NodeHandler;
            preset.OnProcessEdge -= EdgeHandler;
            preset.OnPostRoot -= PostRootHandler;
            preset.OnPostTraversal -= PostTraversalHandler;
        }
        internal void PreTraversalHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset)
        {
        }
        internal void PreRootHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node root)
        {
            AllTIs.Clear();
            TiByNode.Clear();
            NodeByTraversalID.Clear();
        }
        internal void SplitHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int depth)
        {
            //NodeHandler(graph, preset, n, depth);
        }
        internal void EndParallelSegmentHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int depth)
        {
        }
        internal void JoinHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int depth)
        {
            //NodeHandler(graph, preset, n, depth);
        }
        internal void EdgeHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Edge<Node> e, int depth)
        {
            if (string.IsNullOrEmpty(e.Target.TraversalId)) return;
            RegisterNode(e.Source);
            RegisterNode(e.Target);

            TraversalInfo sourceTi = TiByNode[e.Source];
            TraversalInfo targetTi = TiByNode[e.Target];

            if (-1 == sourceTi.Column) sourceTi.Column = 0;
            int nextCol = sourceTi.Column + 1;
            targetTi.Column = e.Target.Model.Category switch
            {
                ModelCategory.Merge => Math.Max(nextCol, targetTi.Column),
                _ => nextCol
            };
        }
        private void RegisterNode(Node n)
        {
            if (string.IsNullOrEmpty(n.TraversalId)) return;
            if (!TiByNode.ContainsKey(n))
            {
                TraversalInfo ti = new TraversalInfo(n);
                AllTIs.Add(ti);
                TiByNode.Add(n, ti);
                NodeByTraversalID.Add(ti.TraversalId, n);
            }
        }
        internal void NodeHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int depth)
        {
            //if (string.IsNullOrEmpty(n.TraversalId)) return;

            //TraversalInfo ti = new TraversalInfo(n);
            //AllTIs.Add(ti);
            //TiByNode.Add(n, ti);
            //NodeByTraversalID.Add(ti.TraversalId, n);
        }
        internal void PostRootHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node root)
        {
            //List<string> allSegments = new(10);
            //foreach (TraversalInfo ti in AllTIs.OrderBy(ti => ti.Path))
            //{
            //    if (!allSegments.Contains(ti.Path))
            //    {
            //        allSegments.Add(ti.Path);
            //        BuildOneParaChainOld(ti.Path);
            //    }
            //}
            List<ModelCategory> nonContent = [ModelCategory.Split, ModelCategory.Input, ModelCategory.Output, ModelCategory.Merge];
            foreach (Node thisSplit in graph.Vertices.Where(n => n.Model.Category == ModelCategory.Split))
            {
                if (graph.Vertices.Where(n => !nonContent.Contains(n.Model.Category) && n.Split == thisSplit).Any())
                {
                    // If there are any "real" content nodes that refer to this
                    // split as a parent, then we need to do a parallelism
                    // signature for this split. That is, we don't need to do
                    // splits that have only other splits or ins/outs on either
                    // of their immediate segments.
                    BuildOneParaChain(TiByNode[thisSplit]);
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
        private void BuildOneParaChain(TraversalInfo tiSplit)
        {


        }
        private void BuildOneParaChainOld(string segmentPath)
        {

            // A is the actual modules on this parallel segment of a split, in
            // order
            List<TraversalInfo> A = AllTIs.Where(ti => ti.Path.Equals(segmentPath)).OrderBy(ti => ti.TraversalId).ToList();
            TraversalInfo tiFirst = A[0];
            if (null == tiFirst.Node.Split) return;
            //if (0 == tiFirst.Node.Depth) return; // this reads clearer but null checking the Split property satisifes compiler nullability checks betters


            // B is the list of modules effectively parallel to the given segment
            // This is modules truly parallel (the other side of the same split our segment came from)
            List<TraversalInfo> B = new(20);
            B.AddRange(AllTIs.Where(ti =>
                    ti.AncestryOfSplits.Equals(tiFirst.AncestryOfSplits) &&
                    !ti.Path.Equals(tiFirst.Path))
                    .OrderBy(ti => ti.TraversalId));


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

            string finalSig = ParaChainSignature(S, A, B, J);
            if (!string.IsNullOrEmpty(finalSig))
                _paraChains.Add($"{segmentPath} {finalSig}");
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
            string? aSeg = null;
            string? bSeg = null; ;
            StringBuilder sb = new(50);
            if (S.Count > 0)
            {
                sb.Append(OneSegment(S));
                sb.Append(" ((( ");
            }
            if (A.Count > 0)
            {
                aSeg = OneSegment(A);
                sb.Append(aSeg);
                sb.Append(" ||| ");
            }
            if (B.Count > 0)
            {
                bSeg = OneSegment(A);
                sb.Append(bSeg);
                sb.Append(" ))) ");
            }
            if (J.Count > 0)
            {
                sb.Append(OneSegment(J));
            }
            if (string.IsNullOrEmpty(aSeg) || string.IsNullOrEmpty(bSeg))
                return "";
            else
                return sb.ToString();
        }
        private static string OneSegment(List<TraversalInfo> list)
        {
            StripNodes(list, [ModelCategory.Split, ModelCategory.Merge]);
            //StripNodes(list, [ModelCategory.Split, ModelCategory.Merge, ModelCategory.Input, ModelCategory.Output]);

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
