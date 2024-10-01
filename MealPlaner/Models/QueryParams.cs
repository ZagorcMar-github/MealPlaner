namespace MealPlaner.Models
{
    public class QueryParams
    {
        public int? Iid {  get; set; }
        public string? Name { get; set; }
        public string[]? Keywords  { get; set; }
        public string[]? Ingredients { get; set; }
        public string[]? ExcludeIngredients { get; set; }

    }
    
}
