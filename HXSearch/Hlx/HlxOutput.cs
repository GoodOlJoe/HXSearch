using Newtonsoft.Json;

namespace HXSearch.Hlx
{
    internal class HlxOutput : HlxBlock
    {
        [JsonProperty("@output")] public readonly int output;
    }
}
