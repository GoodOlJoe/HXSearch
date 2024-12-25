using Newtonsoft.Json;

namespace HXSearch.Hlx
{
    internal class HlxTone
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public HlxToneGlobal global;
        [JsonProperty] private HlxDsp? dsp0;
        [JsonProperty] private HlxDsp? dsp1;
        [JsonIgnore] public List<HlxDsp> Dsp = new(2);
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public void Restructure()
        {
            global?.Restructure();

            if (null != dsp0) { Dsp.Add(dsp0); dsp0 = null; }
            if (null != dsp1) { Dsp.Add(dsp1); dsp1 = null; }

            foreach (HlxDsp dsp in Dsp)
                dsp.Restructure();

        }
    }
}
