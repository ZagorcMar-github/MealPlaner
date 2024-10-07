using MealPlaner.CRUD.Interfaces;
using MealPlaner.Models;
using MealPlaner.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;



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

        public async Task<(bool checkRquest, PagedQuerryResult result)> GetRecipes(QueryParams queryParams,int page=1, int pageSize=10)
        {
            ParallelIngredientFilter parallelFilter = new(_recipesCollection) { };
            List<Recipe> recipes = new List<Recipe>();
            PagedQuerryResult querryResult = new PagedQuerryResult { };
            List<Recipe> keywordFilteredResult = new List<Recipe>();
            int totalItems = 0;
            int totalPages = 0;

            
            var cachingKeyString = "";
            Type type= queryParams.GetType();
            foreach (PropertyInfo property in type.GetProperties())
            {
                var propertyValue= property.GetValue(queryParams, null);
                if (propertyValue != null)
                {
                    Type typeValue = property.PropertyType;
                    if (typeValue.IsArray)
                    {
                        string[] castAraay = (string[])propertyValue;
                        cachingKeyString += $"_{string.Join("_", castAraay.OrderBy(i => i))}";
                    }
                    else 
                    {
                        cachingKeyString += $"_{propertyValue}";
                    }
                }
            }

            string cacheKey = $"{CacheKeyPrefix}{cachingKeyString}";
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

            if (!queryParams.Keywords.IsNullOrEmpty() && queryParams.Ingredients.IsNullOrEmpty())
            {

                keywordFilteredResult= parallelFilter.GetKeywordFilteredRecipes(_recipesCollection, queryParams.Keywords);
                recipes.AddRange(keywordFilteredResult); // doda vse elemente medtem ko add doda samo enga inherently
            }
            else if (queryParams.Keywords.IsNullOrEmpty() && !queryParams.Ingredients.IsNullOrEmpty())
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                keywordFilteredResult = parallelFilter.GetIngredientFilteredRecipes(_recipesCollection, queryParams.Ingredients);
                stopWatch.Stop();
                TimeSpan ts2 = stopWatch.Elapsed;
                Console.WriteLine($"time spent filtering by ingredient with linq : {ts2.TotalSeconds}");

                recipes.AddRange(keywordFilteredResult);

            }
            else if (!queryParams.Keywords.IsNullOrEmpty() && !queryParams.Ingredients.IsNullOrEmpty())
            {
                //keywordFiltering
                
                keywordFilteredResult = parallelFilter.GetKeywordFilteredRecipes(_recipesCollection, queryParams.Keywords);
                


                recipes.AddRange(keywordFilteredResult);
            }
            else 
            {
                var queryableCollection = _recipesCollection.AsQueryable();
                List<Recipe> result = new List<Recipe>();
                result = queryableCollection.ToList();
                recipes.AddRange(result);
                
            }

            List<Recipe> list = recipes;
            if (!queryParams.Ingredients.IsNullOrEmpty()) {
                //applying filtered on results that are allredy in memory
                Stopwatch stopwatch2 = new Stopwatch { };
                stopwatch2.Start();
                list = await parallelFilter.FilterByIngridents(keywordFilteredResult, queryParams,true);
                stopwatch2.Stop();
                Console.WriteLine($"Time spent filtering by ingredient in memory: {stopwatch2.Elapsed}");
            }
            if (!queryParams.ExcludeIngredients.IsNullOrEmpty()) 
            {
                Stopwatch stopwatch3 = new Stopwatch { };
                stopwatch3.Start();
                list =  parallelFilter.FilterByExcludedIngredients(list, queryParams.ExcludeIngredients);
                stopwatch3.Stop();
                Console.WriteLine($"Time spent filtering by excluded in memory: {stopwatch3.Elapsed}");
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


