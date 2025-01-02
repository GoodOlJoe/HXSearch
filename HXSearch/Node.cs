using HXSearch.Hlx;
using HXSearch.Models;

namespace HXSearch
{
    internal class Node
    {
        public Model Model = ModelCatalog.GetModel(ModelId.Unknown.ToString()); // placeholder: non-null field needs a property 
        public HlxBlock Block = new(); // the deserialized json structure from the HLX file

        public int SerialNumber;

        public string Segment = "";
        public int SegmentSequence = 0;
        public string TraversalId => string.IsNullOrEmpty(Segment) ? "" : $"{Segment}-{SegmentSequence}";

        public Node? Split;
        public int OutputPort = -1;
        public override string ToString() => $"{SerialNumber} [T {TraversalId}] {Model} [Split {Split?.SerialNumber}] [Output {OutputPort}]";
        //public override string ToString() => $"{Model} [Split {Split?.SerialNumber}] [Output {OutputPort}]";
        //public override string ToString() => Model.ToString();
    }
}
