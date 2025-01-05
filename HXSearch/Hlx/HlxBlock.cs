using Newtonsoft.Json;

namespace HXSearch.Hlx
{
    internal class HlxBlock
    {
        // self-awareness: am I on dsp0 or dsp1?
        [JsonIgnore] public int DspNum;

        [JsonProperty("@model")] public string model = "";
        [JsonProperty("@Path")] public int path;
        [JsonProperty("@position")] public int position;
        [JsonProperty("@cab")] public string cab = "";
    }
}
