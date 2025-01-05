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
        public int Depth = -1;
        //public override string ToString() => $"{SerialNumber} [T {TraversalId}] {ModelString} [Depth {Depth}] [Output {OutputPort}]";
        public override string ToString() => $"{SerialNumber} [T {TraversalId}] {ModelString} [Split {Split?.SerialNumber}] [Output {OutputPort}]".PadRight(54);
        //public override string ToString() => $"{ModelString} [Split {Split?.SerialNumber}] [Output {OutputPort}]";
        //public override string ToString() => ModelString;
        public string ModelString => Model.Category switch
        {
            ModelCategory.Input => Model.ToString(ModelCatalog.GetInputPortName((InputPortId)((HlxInput)Block).input)),
            ModelCategory.Output => Model.ToString(ModelCatalog.GetOutputPortName((OutputPortId)((HlxOutput)Block).output)),
            _ => Model.ToString()
        };
    }
}
