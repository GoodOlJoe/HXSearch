using Newtonsoft.Json;

namespace HXSearch.Hlx
{
    internal class HlxBlock
    {
        [JsonProperty("@model")] public string model = "";
        [JsonProperty("@path")] public int path;
        [JsonProperty("@position")] public int position;
        [JsonProperty("@cab")] public string cab = "";
    }
}
