using MealPlaner.CRUD.Interfaces;
using MealPlaner.Models;
using MealPlaner.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Diagnostics;



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
            ParallelIngredientFilter parallelFilter = new(_recipesCollection) { };
            List<Recipe> recipes = new List<Recipe>();
            PagedQuerryResult querryResult = new PagedQuerryResult { };
            List<Recipe> keywordFilteredResult = new List<Recipe>();
            int totalItems = 0;
            int totalPages = 0;




            string cacheKey = $"{CacheKeyPrefix}{string.Join("_", queryParams.Ingredients.OrderBy(i => i))}";
            if (pageSize > 100)
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
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                var filter = Builders<Recipe>.Filter.All(x => x.Keywords, queryParams.Keywords);
                keywordFilteredResult = await _recipesCollection.Find(filter)
                        .ToListAsync();

             recipes.AddRange(keywordFilteredResult); // doda vse elemente medtem ko add doda samo enga inherently
             totalItems = (int)await _recipesCollection.CountDocumentsAsync(filter);
             totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
             stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;
                Console.WriteLine($"time spent filteing by keyword : {ts}");
            }
            else
            {
                keywordFilteredResult = await _recipesCollection.Find(Builders<Recipe>.Filter.Empty)
                .ToListAsync();

                recipes.AddRange(keywordFilteredResult);
            }


            List<Recipe> list = recipes;
            if (!queryParams.Ingredients.IsNullOrEmpty()) {
                Stopwatch stopwatch2 = new Stopwatch { };
                stopwatch2.Start();
                list = await parallelFilter.FilterByIngridents(keywordFilteredResult, queryParams,true);
                stopwatch2.Stop();
                Console.WriteLine($"Time spent filtering by ingredient: {stopwatch2.Elapsed}");
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
        
    }
    
}
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}


