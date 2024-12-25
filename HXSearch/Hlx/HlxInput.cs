using Newtonsoft.Json;

namespace HXSearch.Hlx
{
    internal class HlxInput : HlxBlock
    {
        [JsonProperty("@input")] public readonly int input;
    }
}
