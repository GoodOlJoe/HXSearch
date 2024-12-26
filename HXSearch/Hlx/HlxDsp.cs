using Newtonsoft.Json;

namespace HXSearch.Hlx
{
    internal class HlxDsp
    {
        // blocks get deserialized this way, but then I store them in the Blocks
        // list
        [JsonProperty] private HlxBlock? block0;
        [JsonProperty] private HlxBlock? block1;
        [JsonProperty] private HlxBlock? block2;
        [JsonProperty] private HlxBlock? block3;
        [JsonProperty] private HlxBlock? block4;
        [JsonProperty] private HlxBlock? block5;
        [JsonProperty] private HlxBlock? block6;
        [JsonProperty] private HlxBlock? block7;
        [JsonProperty] private HlxBlock? block8;
        [JsonProperty] private HlxBlock? block9;
        [JsonProperty] private HlxBlock? block10;
        [JsonProperty] private HlxBlock? block11;
        [JsonProperty] private HlxBlock? block12;
        [JsonProperty] private HlxBlock? block13;
        [JsonProperty] private HlxBlock? block14;
        [JsonProperty] private HlxBlock? block15;
        [JsonProperty] private HlxBlock? block16;
        [JsonProperty] private HlxBlock? block17;
        [JsonProperty] private HlxBlock? block18;
        [JsonProperty] private HlxBlock? block19;
        [JsonProperty] private HlxBlock? cab0;
        [JsonProperty] private HlxBlock? cab1;
        [JsonProperty] private HlxBlock? cab2;
        [JsonProperty] private HlxBlock? cab3;
        [JsonProperty] private HlxInput? inputA;
        [JsonProperty] private HlxInput? inputB;
        [JsonProperty] private HlxOutput? outputA;
        [JsonProperty] private HlxOutput? outputB;
        [JsonProperty] private HlxSplit? split;
        [JsonProperty] private HlxJoin? join;

        [JsonIgnore] public readonly List<HlxBlock> Blocks = new(20);
        [JsonIgnore] public readonly List<HlxInput> Inputs = new(2);
        [JsonIgnore] public readonly List<HlxOutput> Outputs = new(2);
        [JsonIgnore] public HlxSplit Split = new();
        [JsonIgnore] public HlxJoin Join = new();

        public void Restructure()
        {
            if (null != split) Split = split; split = null;
            if (null != join) Join = join; join = null;

            if (null != inputA) { Inputs.Add(inputA); Inputs[0].inputNum = 0; inputA = null; }
            if (null != inputB) { Inputs.Add(inputB); Inputs[1].inputNum = 1; inputB = null; }

            if (null != outputA) { Outputs.Add(outputA); Outputs[0].outputNum = 0; outputA = null; }
            if (null != outputB) { Outputs.Add(outputB); Outputs[1].outputNum = 1; outputB = null; }

            if (null != block0) { Blocks.Add(block0); block0 = null; }
            if (null != block1) { Blocks.Add(block1); block1 = null; }
            if (null != block2) { Blocks.Add(block2); block2 = null; }
            if (null != block3) { Blocks.Add(block3); block3 = null; }
            if (null != block4) { Blocks.Add(block4); block4 = null; }
            if (null != block5) { Blocks.Add(block5); block5 = null; }
            if (null != block6) { Blocks.Add(block6); block6 = null; }
            if (null != block7) { Blocks.Add(block7); block7 = null; }
            if (null != block8) { Blocks.Add(block8); block8 = null; }
            if (null != block9) { Blocks.Add(block9); block9 = null; }
            if (null != block10) { Blocks.Add(block10); block10 = null; }
            if (null != block11) { Blocks.Add(block11); block11 = null; }
            if (null != block12) { Blocks.Add(block12); block12 = null; }
            if (null != block13) { Blocks.Add(block13); block13 = null; }
            if (null != block14) { Blocks.Add(block14); block14 = null; }
            if (null != block15) { Blocks.Add(block15); block15 = null; }
            if (null != block16) { Blocks.Add(block16); block16 = null; }
            if (null != block17) { Blocks.Add(block17); block17 = null; }
            if (null != block18) { Blocks.Add(block18); block18 = null; }
            if (null != block19) { Blocks.Add(block19); block19 = null; }
        }
    }
}
