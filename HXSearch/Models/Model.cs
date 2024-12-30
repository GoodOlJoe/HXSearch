namespace HXSearch.Models
{
    internal class Model(string Name, ModelId Id, ModelCategory Category, string DisplayName, string BasedOn)
    {
        public readonly string Name = Name;
        public readonly ModelId Id = Id;
        public readonly ModelCategory Category = Category;
        public readonly string DisplayName = DisplayName;
        public readonly string BasedOn = BasedOn;

        public override string ToString()
        {
            return Category == ModelCategory.Unknown ?
                    $"{Category} \"{Name}\"" :
                    $"{Category} \"{DisplayName}\"";
        }
        //public string Signature() => $"c{(int)Category}:m{(int)Id}";
        public string Signature => $"c{Category} m{Id} ";
    }
}
