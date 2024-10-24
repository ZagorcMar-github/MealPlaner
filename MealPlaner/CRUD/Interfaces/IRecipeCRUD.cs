﻿using MealPlaner.Models;
namespace MealPlaner.CRUD.Interfaces
{
    public interface IRecipeCRUD
    {
        public Task<List<RecipeUpdateDto>> CreateRecipe(List<RecipeDto> recipesDto);
        public Task<RecipeUpdateDto> UpdateRecipe(RecipeUpdateDto recipe);
        public Task<RecipeUpdateDto> DeleteRecipe(int id);
        public Task<RecipeUpdateDto> GetRecipe(int id);
        public Task<(bool checkRquest, PagedQuerryResult result)> GetFilteredRecipes(QueryParams queryParams, int page, int pageSize);
        public (bool checkRquest, PagedQuerryResult result)GetRecipes( int page, int pageSize);
        public Task<List<Recipe>> GenerateMealPlan(HttpContext httpContext,DailyMealsDto meals);
        public List<string> GetUniquePreferences();
        public List<string> GetUniqueIngredients();



    }
}
