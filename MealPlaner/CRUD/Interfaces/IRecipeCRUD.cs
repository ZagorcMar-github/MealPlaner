using MealPlaner.Models;
namespace MealPlaner.CRUD.Interfaces
{
    public interface IRecipeCRUD
    {
        public Task<Recipe> CreateRecipe(Recipe recipe);
        public Task<Recipe> UpdateRecipe(Recipe recipe);
        public Task<Recipe> DeleteRecipe(int id);
        public Task<Recipe> GetRecipe(int id);
        public Task<(bool checkRquest, PagedQuerryResult result)> GetRecipes(QueryParams queryParams, int page, int pageSize);
        public Task<List<Recipe>> GenerateMealPlan();
        


    }
}
