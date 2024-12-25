using HXSearch.Models;
using HXSearch.Hlx;

namespace HXSearch
{
    internal sealed class NodeFactory
    {
        // singleton pattern from https://csharpindepth.com/articles/singleton (version #4 in article)

        private static readonly NodeFactory instance = new();

        static NodeFactory() { } // Explicit static constructor to tell C# compiler not to mark type as beforefieldinit
        private NodeFactory() { }
        public static NodeFactory Instance => instance;
        private int SerialNumber = 0;

        public Node NewNode(HlxBlock block)
        {
            return new Node()
            {
                SerialNumber = ++SerialNumber,
                Model = ModelCatalog.GetModel(block.model),
                Block = block,
            };
        }
        public Node NewNode(HlxInput block ) { return NewNode((HlxBlock)block ); }
        public Node NewNode(HlxOutput block ) { return NewNode((HlxBlock)block ); }
        public Node NewNode(HlxSplit block ) { return NewNode((HlxBlock)block ); }
        public Node NewNode(HlxJoin block ) { return NewNode((HlxBlock)block ); }
    }
}
