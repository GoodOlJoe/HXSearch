﻿using HXSearch.Hlx;
using HXSearch.Models;

namespace HXSearch
{
    internal class Node
    {
        public int SerialNumber;
        public Model Model = ModelCatalog.GetModel(ModelId.Unknown.ToString()); // placeholder: non-null field needs a property 
        public HlxBlock Block = new(); // the deserialized json structure from the HLX file
        public Node? Split;
        public override string ToString() => $"{SerialNumber}: {Model.Category} {Model.DisplayName} [{Split?.SerialNumber}]";
    }
}
