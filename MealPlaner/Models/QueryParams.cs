namespace MealPlaner.Models
{
    public class QueryParams
    {
        public int DesiredIngredientPercentage { get; set; } = 55;
        public string[]? Keywords  { get; set; }
        public string[]? Ingredients { get; set; }
        public string[]? ExcludeIngredients { get; set; }

    }
    
}
