using Newtonsoft.Json;

namespace HXSearch.Hlx
{
    internal class HlxToneGlobal
    {
        // would have to add additional topologyN fields to work for devices with more than two DSPs
        [JsonProperty("@topology0")] private string? topology0 = "";
        [JsonProperty("@topology1")] private string? topology1 = "";
        [JsonIgnore] public List<string> Topology = new(2);

        public void Restructure()
        {
            if (null != topology0) { Topology.Add(topology0); topology0 = null; }
            if (null != topology1) { Topology.Add(topology1); topology1 = null; }
        }
    }
}
