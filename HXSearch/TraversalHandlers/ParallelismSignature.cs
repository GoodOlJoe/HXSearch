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
                    BuildParaChainsForOneSplit(TiByNode[thisSplit]);
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
        private void BuildParaChainsForOneSplit(TraversalInfo tiSplit)
        {
            List<ModelCategory> excludeCategories = [ModelCategory.Split, ModelCategory.Merge];

            string firstS = "";
            string firstA = "";
            string firstB = "";
            string firstJ = "";

            // We do a path treating each of this split's segment as the primary
            // path. That's because a parallel signature answers the question
            // "what is parallel to everything on this segment". Since that
            // includes not just hte opposite direct segment but everyting on
            // "outer" parallel segments (in the case of nested parallel
            // segments) the two descendent paths of this split may not be the
            // same
            for (int pathIndex = 0; pathIndex < 2; pathIndex++)
            {

                // the A section is Nodes whose path is {this split's path}.{the current path index 0 or 1}
                List<TraversalInfo> A = AllTIs.Where(ti =>
                    ti.Path.Equals($"{tiSplit.Path}.{pathIndex}") &&
                    !excludeCategories.Contains(ti.Node.Model.Category))
                    .OrderBy(ti => ti.TraversalId).ToList();

                // The S section is nodes N whose path matches the start of this
                // split's path, for the length of N.path and whose column number is
                // less than or equal to this split's column number
                List<TraversalInfo> S = AllTIs.Where(ti =>
                    tiSplit.Path.StartsWith(ti.Path) && ti.Column <= tiSplit.Column &&
                    !excludeCategories.Contains(ti.Node.Model.Category))
                    .OrderBy(ti => ti.TraversalId)
                    .ToList();

                // Matching join is the Node with the same path as this split and sequence number = this split's + 1
                TraversalInfo? tiJoin = AllTIs.Where(ti => ti.Path.Equals(tiSplit.Path) && ti.SegmentSequence == tiSplit.SegmentSequence + 1).FirstOrDefault();

                // The J section is nodes N whose path matches the start of this
                // split's path, for the length of N.path and whose column number is
                // greater than or equal to the matching join's column number
                List<TraversalInfo> J = AllTIs.Where(ti =>
                    tiSplit.Path.StartsWith(ti.Path) && ti.Column >= tiJoin.Column &&
                    !excludeCategories.Contains(ti.Node.Model.Category))
                    .OrderBy(ti => ti.TraversalId)
                    .ToList();

                // NOT SURE ABOUT THESE TWO YET.  SEEMS LIKE THEY ARE ADEQUATELY
                // COVERED BY THE TRUE OR LINEAR PATHS FOR THE QUERIES I IMAGINING
                // SUPPORT

                //// the J section also gets anything WITHIN this split's A path but
                //// downstream of the last node on A. This sounds confusing but it's
                //// anything in a "sub split" of this split's A section but after the
                //// nodes directly on the A path. Those nodes aren't parallel to our
                //// A section, but they need to be represented as downstream of it in
                //// our parallellism signature.
                //int maxAColumn = A.Max(ti => ti.Column);
                //J.AddRange(AllTIs.Where(ti =>
                //    ti.Path.StartsWith(A[0].Path) && ti.Column > maxAColumn &&
                //    !excludeCategories.Contains(ti.Node.Model.Category))
                //    .ToList());

                //// Likewise the S section also gets anything WITHIN this split's A
                //// path but upstream of the first node on A. This will end up being
                //// anything in a "sub split" of this split's A section but before
                //// the nodes directly on the A path. Those nodes aren't parallel to
                //// our A section, but they need to be represented as upstream of
                //// it in our parallellism signature.
                //int minAColumn = A.Min(ti => ti.Column);
                //S.AddRange(AllTIs.Where(ti => ti.Path.StartsWith(A[0].Path) && ti.Column < minAColumn &&
                //    !excludeCategories.Contains(ti.Node.Model.Category))
                //    .ToList());

                // the B section is two things combined...
                //   ...Nodes whose path starts with this split's path but doesn't start with {this split's path}.{this path segment}...
                List<TraversalInfo> B = AllTIs.Where(ti =>
                    ti.Path.StartsWith(tiSplit.Path) && !ti.Path.StartsWith(A[0].Path) &&
                    !excludeCategories.Contains(ti.Node.Model.Category) &&
                    ti.Column > tiSplit.Column &&
                    (null != tiJoin && ti.Column < tiJoin.Column))
                    .OrderBy(ti => ti.TraversalId).ToList();

                //   ...Anything not on this split's A path, and not already on the S, A, B, J sections
                B.AddRange(AllTIs.Where(ti =>
                    !S.Contains(ti) && !A.Contains(ti) && !B.Contains(ti) && !J.Contains(ti) &&
                    !ti.Path.StartsWith(A[0].Path) &&
                    !excludeCategories.Contains(ti.Node.Model.Category))
                    .OrderBy(ti => ti.TraversalId).ToList());

                bool RenderThisSignature = true;
                if (0 == pathIndex)
                {
                    // in many presets, the two descendent parallel segments will yield the same signature, except with
                    // the A and B parts reversed. 
                    firstS = OneSegment(S);
                    firstA = OneSegment(A);
                    firstB = OneSegment(B);
                    firstJ = OneSegment(J);
                }
                else
                {
                    bool sMatch = firstS.Equals(OneSegment(S));
                    bool jMatch = firstJ.Equals(OneSegment(J));

                    string tempA = OneSegment(A);
                    string tempB = OneSegment(B);
                    bool middleMatch = (firstA.Equals(tempA) && firstB.Equals(tempB)) || (firstA.Equals(tempB) && firstB.Equals(tempA));

                    bool SegmentsAreTheSame = sMatch && jMatch && middleMatch;
                    RenderThisSignature = !SegmentsAreTheSame;
                }

                if (RenderThisSignature)
                {
                    string finalSig = ParaChainSignature(S, A, B, J);
                    if (!string.IsNullOrEmpty(finalSig))
                        _paraChains.Add($"{tiSplit.Path} {finalSig}");
                }
            }
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
            //string? aSeg = null;
            //string? bSeg = null;
            StringBuilder sb = new(50);
            if (S.Count > 0)
            {
                sb.Append(OneSegment(S));
            }
            sb.Append(" ((( ");
            if (A.Count > 0)
            {
                //aSeg = OneSegment(A);
                //sb.Append(aSeg);
                sb.Append(OneSegment(A));
            }
            sb.Append(" ||| ");
            if (B.Count > 0)
            {
                //bSeg = OneSegment(B);
                //sb.Append(bSeg);
                sb.Append(OneSegment(B));
            }
            sb.Append(" ))) ");
            if (J.Count > 0)
            {
                sb.Append(OneSegment(J));
            }
            //if (string.IsNullOrEmpty(aSeg) || string.IsNullOrEmpty(bSeg))
            //    return "";
            //else
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
