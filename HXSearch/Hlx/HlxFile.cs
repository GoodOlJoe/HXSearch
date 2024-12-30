using Newtonsoft.Json;
using System.Text;

namespace HXSearch.Hlx
{
    internal class HlxFile
    {
        public HlxData data = new();
        public string schema = "";
        public float version;
        public bool Loaded => !string.IsNullOrEmpty(schema);
        public void Restructure()
        {
            data?.Restructure(); // protected or encrypted presets won't have a data section
        }
        public static HlxFile Load(string fqn)
        {
            string json = File.ReadAllText(fqn, Encoding.UTF8);
            var f = JsonConvert.DeserializeObject<HlxFile>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None });
            if (f != null)
            {
                f.Restructure();
                return f;
            }
            else
                return new HlxFile(); // avoiding nullability problems: return a non-loaded one...they can check .Loaded
        }
    }
}
