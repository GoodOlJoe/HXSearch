namespace HXSearch.Hlx
{
    internal class HlxData
    {
        public int device;
        public int device_version;
        public HlxDataMeta meta = new();
        public HlxTone tone = new();
        public void Restructure()
        {
            tone.Restructure();
        }
    }
}
