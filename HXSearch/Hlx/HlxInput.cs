using Newtonsoft.Json;

namespace HXSearch.Hlx
{
    internal class HlxInput : HlxConnector
    {
        // self-awareness: am I on dsp0 or dsp1?
        [JsonIgnore] public int dspNum;

        // self-awareness: am I inputA (0) or inputB (1)? Not to be confused
        // with the input property which indicates where the input comes from on
        // the device
        [JsonIgnore] public int inputNum;

        [JsonProperty("@input")] public readonly int input;
    }
}
