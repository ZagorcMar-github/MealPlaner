namespace MealPlaner.Models
{
    public class RecipeDto
    {
        public  string Name { get; set; }
        public string? CookTime { get; set; }
        public string? PrepTime { get; set; }
        public string? TotalTime { get; set; }
        public  string RecipeCategory { get; set; }
        public  List<string> Keywords { get; set; }
        public   List<string> RecipeIngredientParts { get; set; }
        public  double TotalCalories { get; set; }
        public double TotalFatContent { get; set; }
        public double TotalSaturatedFatContent { get; set; }
        public double TotalCholesterolContent { get; set; }
        public double TotalSodiumContent { get; set; }
        public double TotalCarbohydrateContent { get; set; }
        public  double TotalFiberContent { get; set; }
        public  double TotalSugarContent { get; set; }
        public  double TotalProteinContent { get; set; }
        public  int RecipeServings { get; set; }
        public string? RecipeYield { get; set; }
        public  List<string> RecipeInstructions { get; set; }
        public  List<string> ingredients_raw { get; set; }
        public RecipeDto() { }
        public RecipeDto(Recipe recipe)
        {
            Name = recipe.Name;
            CookTime =recipe.CookTime ;
            PrepTime = recipe.PrepTime;
            TotalTime = recipe.TotalTime;
            RecipeCategory = recipe.RecipeCategory;
            Keywords = recipe.Keywords;
            RecipeIngredientParts = recipe.RecipeIngredientParts;
            TotalCalories = recipe.Calories;
            TotalFatContent = recipe.FatContent;
            TotalSaturatedFatContent = recipe.SaturatedFatContent;
            TotalCholesterolContent = recipe.CholesterolContent;
            TotalSodiumContent = recipe.SodiumContent;
            TotalCarbohydrateContent = recipe.CarbohydrateContent;
            TotalFiberContent = recipe.FiberContent;
            TotalSugarContent = recipe.SugarContent;
            TotalProteinContent = recipe.ProteinContent;
            RecipeServings = recipe.RecipeServings;
            RecipeYield = recipe.RecipeYield;
            RecipeInstructions = recipe.RecipeInstructions;
            ingredients_raw=recipe.ingredients_raw;
            
        }

    }
}
