namespace MealPlaner.Models
{
    public class RecipesDatabaseSettings
    {
        public string ConnectionString { get; set; } = null!;

        public string DatabaseName { get; set; } = null!;

        public string RecipesCollectionName { get; set; } = null!;
        public string UsersCollectionName { get; set; } = null!;
    }
}
