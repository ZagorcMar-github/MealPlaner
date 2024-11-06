namespace MealPlaner.Models
{
    public class PagedQuerryResult
    {
        public List<RecipeUpdateDto>? Recipes { get; set; }
        public int? Page { get; set; }
        public int? TotalPages { get; set; }
        public int? PageSize { get; set; }
        public int? TotalItems { get; set; }
    }
}
