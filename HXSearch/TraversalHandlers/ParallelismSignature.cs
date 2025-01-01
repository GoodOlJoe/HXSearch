using HXSearch.Models;
using QuikGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static HXSearch.Preset;

namespace HXSearch.TraversalHandlers
{
    internal class ParallelismSignature
    {
        private class ParaTag
        {
            public string Tag = "0";
            public int Seq = 0;
            public Node Node = new();
            public string Signature => $"{Tag}-{Seq}";
        }

        private readonly List<ParaTag> TaggedNodes = new(50);
        private readonly Stack<ParaTag> Stack = new(10);
        private string tag = "0";
        private int nextSequence = 1;

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
        internal void SplitHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            ParaTag taggedSplit = new() { Tag = tag, Seq = nextSequence, Node = n };
            TaggedNodes.Add(taggedSplit);

            Stack.Push(taggedSplit.Signature); // push the tag of the split itself

            List<Edge<Node>> outEdges = graph.OutEdges(n).ToList();
            for (int i = 1; i < outEdges.Count; i++)
                Stack.Push($"{tag}.{i+1}-1"); // a tag representing the first item on each of this's out edges (other than the first
            tag = $"{tag}.1";
            nextSequence = 1;
        }
        internal void EndParallelSegmentHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            if (0 == Stack.Count) return; // join without a preceding split
            tag = Stack.Pop();
        }
        internal void JoinHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            if (0 == Stack.Count) return; // join without a preceding split
            tag = Stack.Pop(); // this will be the split itself
            //nextSequence = TaggedNodes[tag].Seq;


        }
        internal void NodeHandler(AdjacencyGraph<Node, Edge<Node>> graph, Preset preset, Node n, int splitLevel)
        {
            if (null == n) return;
            TaggedNodes.Add(new ParaTag() { Tag = tag, Seq = nextSequence, Node = n });
            nextSequence++;
        }
    }
}
