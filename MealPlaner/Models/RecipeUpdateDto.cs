namespace MealPlaner.Models
{
    public class RecipeUpdateDto : RecipeDto
    {
        public  int RecipeId { get; set; }

        public RecipeUpdateDto() : base() { }  // Default constructor

        public RecipeUpdateDto(Recipe recipe) : base(recipe)
        {
            RecipeId = recipe.RecipeId;
        }
    }
}
