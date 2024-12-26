namespace HXSearch.Models
{
    internal class Model
    {
        public readonly string Name;
        public readonly ModelId Id;
        public readonly ModelCategory Category = ModelCategory.Unknown;
        public readonly string DisplayName;
        public readonly string BasedOn;

        public Model(string Name, ModelId Id, ModelCategory Category, string DisplayName, string BasedOn)
        {
            this.Name = Name;
            this.Id = Id;
            this.Category = Category;
            this.BasedOn = BasedOn;
            this.DisplayName = DisplayName;
        }
        public override string ToString()
        {
            return Category == ModelCategory.Unknown ?
                    $"{Category} \"{Name}\"" :
                    $"{Category} \"{DisplayName}\"";
        }
    }
}
