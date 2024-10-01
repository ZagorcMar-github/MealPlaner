using MealPlaner.CRUD.Interfaces;
using MealPlaner.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;



namespace MealPlaner.CRUD
{
    public class RecipeCRUD : IRecipeCRUD
    {
        private readonly IMongoCollection<Recipe> _recipesCollection;
        private readonly ILogger _logger;
        private readonly IMemoryCache _cache;
        private const string CacheKeyPrefix = "FilteredRecipes_";
        public RecipeCRUD(IOptions<RecipesDatabaseSettings> recipesDatabaseSettings,ILogger<RecipeCRUD> logger,IMemoryCache cache)
        {
            var mongoClient = new MongoClient(
            recipesDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                recipesDatabaseSettings.Value.DatabaseName);

            _recipesCollection = mongoDatabase.GetCollection<Recipe>(
                recipesDatabaseSettings.Value.RecipesCollectionName);
            _logger = logger;
            _cache = cache;
        }

        public async Task<List<Recipe>> GenerateMealPlan()
        {
            try {
                List<Recipe>? recipes = new List<Recipe> { };
                return  recipes;
            } catch(Exception ex) {
                throw; 
            }
            
        }

        public async Task<Recipe> CreateRecipe(Recipe recipe)
        {
            throw new NotImplementedException();
        }

        public Task<Recipe> DeleteRecipe(int id)
        {
            throw new NotImplementedException();
        }

        public Task<Recipe[]> GetMealPlan()
        {
            throw new NotImplementedException();
        }
        
        public async Task<Recipe> GetRecipe(int id)
        {
            try {
                var result= await _recipesCollection.Find(x=>x.RecipeId == id).FirstOrDefaultAsync();
                
                    return result;
               
            }
            catch (Exception ex) {

                _logger.LogInformation(ex.Message);
                throw;
            } 
           
        }

        public async Task<(bool checkRquest, PagedQuerryResult result)> GetRecipes(QueryParams queryParams,int page, int pageSize)
        {
            List<Recipe> recipes = new List<Recipe>();
            PagedQuerryResult querryResult = new PagedQuerryResult { };
            List<Recipe> keywordFilteredResult = new List<Recipe>();
            int totalItems = 0;
            int totalPages = 0;




            string cacheKey = $"{CacheKeyPrefix}{string.Join("_", queryParams.Ingredients.OrderBy(i => i))}";
            if (pageSize > 1000)
            {
                return (false, querryResult);
            }

            if (_cache.TryGetValue(cacheKey, out List<Recipe> filteredRecipes))
            {

                
                List<Recipe> cachedFilteredRecipes = filteredRecipes.Skip((page - 1) * pageSize).Take(pageSize).ToList();


                PagedQuerryResult cachedFilteredResult = new PagedQuerryResult
                {
                    Recipes=cachedFilteredRecipes,
                    Page = page,
                    PageSize = pageSize,
                    TotalItems = filteredRecipes.Count,
                    TotalPages= (int)Math.Ceiling((double)filteredRecipes.Count / pageSize)
                };
                return (true, cachedFilteredResult);
            
            }



           
            

            if (!queryParams.Keywords.IsNullOrEmpty())
            {
            var filter = Builders<Recipe>.Filter.All(x => x.Keywords, queryParams.Keywords);
                keywordFilteredResult = await _recipesCollection.Find(filter).Skip((page - 1) * pageSize)
                        .Limit(pageSize)
                        .ToListAsync();

             recipes.AddRange(keywordFilteredResult); // doda vse elemente medtem ko add doda samo enga inherently
             totalItems = (int)await _recipesCollection.CountDocumentsAsync(filter);
             totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            Console.WriteLine("came");
            }
            else
            {
                keywordFilteredResult = await _recipesCollection.Find(Builders<Recipe>.Filter.Empty)
                .ToListAsync();

                recipes.AddRange(keywordFilteredResult);
            }



            List<Recipe> list = recipes;
            if (!queryParams.Ingredients.IsNullOrEmpty()) {
                list = await FilterByIngridents(keywordFilteredResult, queryParams);
            }
            

            List<Recipe> pagedList =list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            querryResult.TotalItems = list.Count;
            querryResult.TotalPages = (int)Math.Ceiling((double)list.Count / pageSize); ;
            querryResult.PageSize = pageSize;
            querryResult.Page = page;
            querryResult.Recipes = pagedList;
            



            var cacheEntryOptions = new MemoryCacheEntryOptions()
               .SetSlidingExpiration(TimeSpan.FromMinutes(30));
            _cache.Set(cacheKey, list, cacheEntryOptions);
            return   (true, querryResult);
        }






        public Task<Recipe> UpdateRecipe(Recipe recipe)
        {
            throw new NotImplementedException();
        }
        public async Task <List<Recipe>> FilterByIngridents(List<Recipe> recipes,QueryParams queryParams) 
        {
            var ingredientFilters = queryParams.Ingredients
        .Select(ingredient => Builders<Recipe>.Filter.Regex(
            x => x.ingredients_raw,
            new MongoDB.Bson.BsonRegularExpression(ingredient, "i"))) 
        .ToList();


            var filter = Builders<Recipe>.Filter.Or(ingredientFilters);


             recipes = await _recipesCollection.Find(filter)
                .ToListAsync();


            var filteredRecipes =  FilterRecipesByIngredientMatch(recipes, queryParams.Ingredients);
            Console.WriteLine("completed filtering by ingredient");
            return filteredRecipes;
        }
        private List<Recipe> FilterRecipesByIngredientMatch(List<Recipe> recipes, string[] queryIngredients)
        {
            return recipes.Where(recipe =>
            {
                int matchCount = queryIngredients.Count(ingredient =>
                    recipe.ingredients_raw.Any(recipeIngredient =>
                        recipeIngredient.IndexOf(ingredient, StringComparison.OrdinalIgnoreCase) >= 0));

                double matchPercentage = (double)matchCount / queryIngredients.Length * 100;
                return matchPercentage >= 70;
            }).ToList();
        }
    }
    
}
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}


