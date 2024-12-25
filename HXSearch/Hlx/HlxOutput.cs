using Newtonsoft.Json;

namespace HXSearch.Hlx
{
    internal class HlxOutput : HlxConnector
    {
        // self-awareness: am I on dsp0 or dsp1?
        [JsonIgnore] public int dspNum;

        // self-awareness: am I outputA (0) or outputB (1)? Not to be confused
        // with the output property which indicates where the output goes on the
        // device
        [JsonIgnore] public int outputNum;


        [JsonProperty("@output")] public readonly int output;
    }
}
