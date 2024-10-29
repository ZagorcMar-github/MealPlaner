using MealPlaner.CRUD.Interfaces;
using MealPlaner.Models;
using MealPlaner.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;



namespace MealPlaner.CRUD
{
    public class RecipeCRUD : IRecipeCRUD
    {
        private readonly IMongoCollection<Recipe> _recipesCollection;
        private readonly ILogger _logger;
        private readonly IMemoryCache _cache;
        private readonly IUserCRUD _userCRUD;
        private readonly HeaderRequestDecoder _headerRequestDecoder;

        private const string CacheKeyPrefix = "FilteredRecipes_";
        public RecipeCRUD(IOptions<RecipesDatabaseSettings> recipesDatabaseSettings, ILogger<RecipeCRUD> logger, IMemoryCache cache,IUserCRUD userCRUD,HeaderRequestDecoder headerRequestDecoder)
        {
            var mongoClient = new MongoClient(
            recipesDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                recipesDatabaseSettings.Value.DatabaseName);

            _recipesCollection = mongoDatabase.GetCollection<Recipe>(
                recipesDatabaseSettings.Value.RecipesCollectionName);
            _logger = logger;
            _cache = cache;
            _userCRUD=userCRUD;
            _headerRequestDecoder= headerRequestDecoder;    
        }

        public async Task<List<Recipe>> GenerateMealPlan(HttpContext httpContext,MealsDto meals)
        {
            try {
                int userId = 0;
                Int32.TryParse(_headerRequestDecoder.ExtractUserIdFromJwt(httpContext), out userId);
                if (userId <= 0) 
                {
                    return new List<Recipe>();
                }

                ParallelIngredientFilter filter = new ParallelIngredientFilter(_recipesCollection);

                UserResponseDto user= await _userCRUD.GetUser(userId);
                int[] prev5UsedRecipes = user.PreviusRecipeIds.TakeLast(5).ToArray();
                
                List<Recipe> baseRecipes = GlobalVariables.Recipes;
                List<Recipe>? optimalRecipes = new List<Recipe> { };
                List<Recipe>? keywordFilteredRecipes = new List<Recipe> { };
                baseRecipes = baseRecipes.Where(x => !prev5UsedRecipes.Contains(x.RecipeId)).ToList();
                if (!meals.Preferences.IsNullOrEmpty())
                {
                    baseRecipes = await filter.FilterByKeywords(baseRecipes, meals.Preferences);
                }

                CreateRangedProcentageValues(meals.Meals); // creates a procentage value for each meal that coresponds to the amount of meals a day.
                                                                // the next iteration would take in to account the remaing nutritional amount neaded
                                                                // so if i user puts breakfast (calories) 0.3 lunch 0.3 and  dinner: 0.3
                                                                // the ranged characteristics would return breakfast (calories) 0.3 lunch 0.67 and dinner: 1
                                                                // if we add another meal split in to 4 equal percentile pieces like:
                                                                //                   breakfast (calories) 0.25 lunch: 0.50 dinner:0.75 and snack:1  
                                                                // thus we ensure a slight bit of variation to the generated recipes in each iteration 

                foreach (var (key, mealCharacteristics) in meals.Meals)
                {
                    var mealRecipes = baseRecipes;

                    if (!mealCharacteristics.MustInclude.IsNullOrEmpty()) 
                    {
                        mealRecipes = filter.FilterByMustIncludeIngredients(mealRecipes, mealCharacteristics.MustInclude.ToArray());
                    }
                    if (!mealCharacteristics.MustExclude.IsNullOrEmpty()) 
                    {
                        mealRecipes = filter.FilterByExcludedIngredients(mealRecipes, mealCharacteristics.MustExclude.ToArray());
                    }
                    var MealNutritionalGoal = getRawNutritionalValue(meals.Goals, mealCharacteristics); // get the amount of (raw non procentile) nutrition a meal should consist of
                    await NormalizeNutritionalValues(MealNutritionalGoal);
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    stopwatch.Start();
                    var foundRecipe = FindOptimalRecipe(mealRecipes, MealNutritionalGoal);
                    stopwatch.Stop();
                    Console.WriteLine($"time elapsed finding optimar recipe goals: {stopwatch.Elapsed}");
                    if (foundRecipe != null)
                    {
                        meals.Goals.TargetCarbohydrateContent = meals.Goals.TargetCarbohydrateContent - foundRecipe.CarbohydrateContent / foundRecipe.RecipeServings;
                        meals.Goals.TargetSaturatedFatContent = meals.Goals.TargetSaturatedFatContent - foundRecipe.SaturatedFatContent / foundRecipe.RecipeServings;
                        meals.Goals.TargetCholesterolContent = meals.Goals.TargetCholesterolContent - foundRecipe.CholesterolContent / foundRecipe.RecipeServings;
                        meals.Goals.TargetProteinContent = meals.Goals.TargetProteinContent - foundRecipe.ProteinContent / foundRecipe.RecipeServings; 
                        meals.Goals.TargetFiberContent = meals.Goals.TargetFiberContent - foundRecipe.FiberContent / foundRecipe.RecipeServings;
                        meals.Goals.TargetSugarContent = meals.Goals.TargetSugarContent - foundRecipe.SugarContent / foundRecipe.RecipeServings;
                        meals.Goals.TargetFatContent = meals.Goals.TargetFatContent - foundRecipe.FatContent / foundRecipe.RecipeServings;
                        meals.Goals.TargetCalories = meals.Goals.TargetCalories - foundRecipe.Calories / foundRecipe.RecipeServings;
                    }
                    optimalRecipes.Add(foundRecipe);

                }
                return optimalRecipes;
            }
            catch (Exception ex)
            {
                throw;
            }

        }
        private Recipe FindOptimalRecipe(List<Recipe> recipes, NutritionalGoals normalizedNutritionalGoals)
        {
            var recs = recipes.Select(recipe =>
            {
                var RecipeProperties = typeof(Recipe).GetProperties().Where(p => p.Name.EndsWith("_MinMax"));
                var normalizedNutritionalGoalsProperties = typeof(NutritionalGoals).GetProperties();
                Dictionary<string, object> propertyDictionary = new Dictionary<string, object>();
                double euclidianDistance = 0;
                double sumOfSquares = 0;
                foreach (var property in RecipeProperties)
                {
                    if (property.CanRead) 
                    {
                        propertyDictionary[property.Name] = property.GetValue(recipe);
                    }
                }
                foreach (var property in normalizedNutritionalGoalsProperties)
                {
                    var normTargetNutritionalValue = (double)property.GetValue(normalizedNutritionalGoals);
                    string key = $"{property.Name.Replace("Target", "")}_MinMax";
                    if (propertyDictionary.TryGetValue(key, out object RecipeNutValue))
                    {
                        if (double.TryParse(RecipeNutValue?.ToString(), out double recipeNutValueParsed))
                        {
                            sumOfSquares += Math.Pow((recipeNutValueParsed - normTargetNutritionalValue), 2);
                        };
                    }
                }
                euclidianDistance = Math.Sqrt(sumOfSquares);
                return new { Recipe = recipe, EuclidianDistance = euclidianDistance };
            }).OrderBy(x => x.EuclidianDistance).Take(5).ToList();
            var random = new Random();
            Recipe optimalRecipe = new Recipe { };
            if (!recs.IsNullOrEmpty()) 
            {
            optimalRecipe = recs[random.Next(recs.Count)].Recipe;
            }
            return optimalRecipe;

        }
        private NutritionalGoals getRawNutritionalValue(NutritionalGoals dailyGoal, MealCharacteristics mealCharacteristics)
        {
            NutritionalGoals mealGoal = new NutritionalGoals
            {
                TargetCalories = dailyGoal.TargetCalories * mealCharacteristics.TargetCalorieProcent,
                TargetCarbohydrateContent = dailyGoal.TargetCarbohydrateContent * mealCharacteristics.TargetCarbohydrateProcent,
                TargetCholesterolContent = dailyGoal.TargetCholesterolContent * mealCharacteristics.TargetCholesterolProcent,
                TargetFatContent = dailyGoal.TargetFatContent * mealCharacteristics.TargetFatProcent,
                TargetFiberContent = dailyGoal.TargetFiberContent * mealCharacteristics.TargetFiberProcent,
                TargetProteinContent = dailyGoal.TargetProteinContent * mealCharacteristics.TargetProteinProcent,
                TargetSaturatedFatContent = dailyGoal.TargetSaturatedFatContent * mealCharacteristics.TargetSaturatedFatProcent,
                TargetSugarContent = dailyGoal.TargetSugarContent * mealCharacteristics.TargetSugarProcent,

            };
            return mealGoal;
        }
        private async Task<bool> NormalizeNutritionalValues(NutritionalGoals rawMealNutritionalValues)
        {
            try
            {
                Stopwatch stopwatch = new Stopwatch { };
                stopwatch.Start();
                var properties = typeof(NutritionalGoals).GetProperties();
                foreach (var item in properties)
                {
                    var propertyName = item.Name;

                    
                    if (propertyName.StartsWith("Target"))
                    {
                        propertyName = propertyName.Substring("Target".Length);
                    }

                    stopwatch.Start();
                    var minValue = await GetMinValueAsync(propertyName);
                    stopwatch.Stop();
                    //Console.WriteLine($"Time Elapsed getting min value {stopwatch.Elapsed}");
                    //Stopwatch stopwatch1 = new Stopwatch();
                    //stopwatch1.Start();
                    var maxValue = await GetMaxValueAsync(propertyName);
                    //stopwatch1.Stop();
                    //Console.WriteLine($" Time Elapsed getting max value: {stopwatch1.Elapsed}");
                    var orginalValue = (double)item.GetValue(rawMealNutritionalValues);
                    item.SetValue(rawMealNutritionalValues, getNormalizedValue(orginalValue, maxValue, minValue, 1.0));
                }
                stopwatch.Stop();
                Console.WriteLine($"time spent normalizing nutritional values: {stopwatch.Elapsed}");
                return true;
            }
            catch (Exception ex)
            {
                return false;

            }
        }

        private void CreateRangedProcentageValues(Dictionary<string, MealCharacteristics> mealsCharacteristics)
        {
            Stopwatch stopwatch = new Stopwatch { };
            stopwatch.Start();
            int mealsADay = mealsCharacteristics.Count;
            var properties = typeof(MealCharacteristics).GetProperties()
                .Where(p => p.Name.StartsWith("Target") && p.Name.EndsWith("Procent"));
            foreach (var prop in properties)
            {
                int index = 0;
                double mealTargetProcentSum = 0;
                foreach (var (key, value) in mealsCharacteristics)
                {

                    var meal = value;
                    if (index == mealsADay - 1) 
                    {
                        prop.SetValue(meal, 1);
                    }
                    else
                    {
                        var CurrentValue= (double)prop.GetValue(meal);
                        var rangedValue = CurrentValue / (1 - mealTargetProcentSum);
                        mealTargetProcentSum += CurrentValue;
                        prop.SetValue(meal, rangedValue);    
                    }
                    index++;

                }
            }
            stopwatch.Stop();
            Console.WriteLine($"time spent normalizing creating ranged values: {stopwatch.Elapsed}");
        }

        public async Task<List<RecipeUpdateDto>> CreateRecipe(List<RecipeDto> recipesDto)
        {
            var CaloriesMaxValue = await GetMaxValueAsync("Calories");
            var CaloriesMinValue = await GetMinValueAsync("Calories");
            var FatContentMaxValue = await GetMaxValueAsync("FatContent");
            var FatContentMinValue = await GetMinValueAsync("FatContent");
            var SaturatedFatContentMaxValue = await GetMaxValueAsync("SaturatedFatContent");
            var SaturatedFatContentMinValue = await GetMinValueAsync("SaturatedFatContent");
            var CholesterolContentMaxValue = await GetMaxValueAsync("CholesterolContent");
            var CholesterolContentMinValue = await GetMinValueAsync("CholesterolContent");
            var SodiumContentMaxValue = await GetMaxValueAsync("SodiumContent");
            var SodiumContentMinValue = await GetMinValueAsync("SodiumContent");
            var CarbohydrateContentMaxValue = await GetMaxValueAsync("CarbohydrateContent");
            var CarbohydrateContentMinValue = await GetMinValueAsync("CarbohydrateContent");
            var FiberContentMaxValue = await GetMaxValueAsync("FiberContent");
            var FiberContentMinValue = await GetMinValueAsync("FiberContent");
            var SugarContentMaxValue = await GetMaxValueAsync("SugarContent");
            var SugarContentMinValue = await GetMinValueAsync("SugarContent");
            var ProteinContentMaxValue = await GetMaxValueAsync("ProteinContent");
            var ProteinContentMinValue = await GetMinValueAsync("ProteinContent");
            var lastId = System.Convert.ToInt32(System.Math.Floor(await GetMaxValueAsync("RecipeId")));
            List<Recipe> recipesToInsert = new List<Recipe>();
            List<RecipeUpdateDto> recipesResponse = new List<RecipeUpdateDto>();
            foreach(var recipeDto in recipesDto)
            {
                Recipe recipe = new Recipe
                {
                    RecipeId = (lastId + 1),
                    Name = recipeDto.Name,
                    Keywords = recipeDto.Keywords,
                    RecipeCategory = recipeDto.RecipeCategory,
                    ingredients_raw = recipeDto.ingredients_raw,
                    RecipeIngredientParts = recipeDto.RecipeIngredientParts,
                    RecipeInstructions = recipeDto.RecipeInstructions,
                    RecipeServings = recipeDto.RecipeServings,
                    CookTime = recipeDto.CookTime ?? "",
                    RecipeYield = recipeDto.RecipeYield ?? "",
                    PrepTime = recipeDto.PrepTime ?? "",
                    TotalTime = recipeDto.TotalTime ?? "",


                    Calories = recipeDto.TotalCalories,
                    FatContent = recipeDto.TotalFatContent,
                    SaturatedFatContent = recipeDto.TotalSaturatedFatContent,
                    CholesterolContent = recipeDto.TotalCholesterolContent,
                    SodiumContent = recipeDto.TotalSodiumContent,
                    CarbohydrateContent = recipeDto.TotalCarbohydrateContent,
                    FiberContent = recipeDto.TotalFiberContent,
                    SugarContent = recipeDto.TotalSugarContent,
                    ProteinContent = recipeDto.TotalProteinContent,

                    Calories_MinMax = getNormalizedValue(recipeDto.TotalCalories, CaloriesMaxValue, CaloriesMinValue, recipeDto.RecipeServings),
                    FatContent_MinMax = getNormalizedValue(recipeDto.TotalFatContent, FatContentMaxValue, FatContentMinValue, recipeDto.RecipeServings),
                    SaturatedFatContent_MinMax = getNormalizedValue(recipeDto.TotalSaturatedFatContent, SaturatedFatContentMaxValue, SaturatedFatContentMinValue, recipeDto.RecipeServings),
                    CholesterolContent_MinMax = getNormalizedValue(recipeDto.TotalCholesterolContent, CholesterolContentMaxValue, CholesterolContentMinValue, recipeDto.RecipeServings),
                    SodiumContent_MinMax = getNormalizedValue(recipeDto.TotalSodiumContent, SodiumContentMaxValue, SodiumContentMinValue, recipeDto.RecipeServings),
                    CarbohydrateContent_MinMax = getNormalizedValue(recipeDto.TotalCarbohydrateContent, CarbohydrateContentMaxValue, CarbohydrateContentMinValue, recipeDto.RecipeServings),
                    FiberContent_MinMax = getNormalizedValue(recipeDto.TotalFiberContent, FiberContentMaxValue, FiberContentMinValue, recipeDto.RecipeServings),
                    SugarContent_MinMax = getNormalizedValue(recipeDto.TotalSugarContent, SugarContentMaxValue, SugarContentMinValue, recipeDto.RecipeServings),
                    ProteinContent_MinMax = getNormalizedValue(recipeDto.TotalProteinContent, ProteinContentMaxValue, ProteinContentMinValue, recipeDto.RecipeServings),
                };
                recipesToInsert.Add(recipe);
                var cleanRecipe = new RecipeUpdateDto(recipe);
                recipesResponse.Add(cleanRecipe);

            }

            await _recipesCollection.InsertManyAsync(recipesToInsert);

            return recipesResponse;
        }
        private double getNormalizedValue(double originalValue, double maxValue, double minValue, double recipeServings)
        {
            try
            {
                double normalizedValue = (maxValue != minValue)
                     ? (originalValue / recipeServings - minValue) / (maxValue - minValue) : 0;

                return normalizedValue;
            }
            catch (Exception)
            {

                throw;
            }

        }


        public async Task<RecipeUpdateDto> DeleteRecipe(int id)
        {
            try
            {
                var deletedRecipe = await GetRecipe(id);
                var filter = Builders<Recipe>.Filter.Eq(r => r.RecipeId, id);
                await _recipesCollection.DeleteOneAsync(filter);
                return deletedRecipe;
            }
            catch (Exception ex)
            {
                _logger.LogError($"exception during deletion : {ex.Message}");
                throw;
            }

        }

        public Task<Recipe[]> GetMealPlan()
        {
            throw new NotImplementedException();
        }
        public async Task<List<RecipeUpdateDto>> GetRecipeByName(string recipeName)
        {
            var recipes = await _recipesCollection.Find(x => x.Name.Contains(recipeName)).ToListAsync();
            List<RecipeUpdateDto> recipesResponse= new List<RecipeUpdateDto>(); 
            foreach (var recipe in recipes)
            {
                RecipeUpdateDto recipeResponse = new RecipeUpdateDto(recipe);
                recipesResponse.Add(recipeResponse);
            }
            return recipesResponse;
        }
        public async Task<RecipeUpdateDto> GetRecipe(int id)
        {
            try
            {
                var result = await _recipesCollection.Find(x => x.RecipeId == id).FirstOrDefaultAsync(); 
                RecipeUpdateDto cleanResult = new RecipeUpdateDto(result);
                return cleanResult;

            }
            catch (Exception ex)
            {

                _logger.LogInformation(ex.Message);
                throw;
            }

        }


        public async Task<(bool checkRquest, PagedQuerryResult result)> GetFilteredRecipes(QueryParams queryParams, int page = 1, int pageSize = 10)
        {
            try
            {
                ParallelIngredientFilter parallelFilter = new(_recipesCollection) { };
                List<Recipe> recipes = new List<Recipe>();
                PagedQuerryResult querryResult = new PagedQuerryResult { };
                List<Recipe> filteredList = GlobalVariables.Recipes;
                List<Recipe> keywordFilteredResult = new List<Recipe>();
                int totalItems = 0;
                int totalPages = 0;


                var cachingKeyString = "";
                Type type = queryParams.GetType();
                if (pageSize > 100)
                {
                    return (false, querryResult);
                }
                foreach (PropertyInfo property in type.GetProperties())
                {
                    var propertyValue = property.GetValue(queryParams, null);
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

                string cacheKey = $"{CacheKeyPrefix}{cachingKeyString}_{pageSize}";

                if (_cache.TryGetValue(cacheKey, out List<Recipe> filteredRecipes))
                {


                    List<Recipe> cachedFilteredRecipes = filteredRecipes.Skip((page - 1) * pageSize).Take(pageSize).ToList();


                    PagedQuerryResult cachedFilteredResult = new PagedQuerryResult
                    {
                        Recipes = cachedFilteredRecipes,
                        Page = page,
                        PageSize = pageSize,
                        TotalItems = filteredRecipes.Count,
                        TotalPages = (int)Math.Ceiling((double)filteredRecipes.Count / pageSize)
                    };
                    return (true, cachedFilteredResult);

                }


                if (!queryParams.Keywords.IsNullOrEmpty())
                {
                    //applying filtered on results that are allredy in memory
                    Stopwatch stopwatch2 = new Stopwatch { };
                    stopwatch2.Start();
                    filteredList = await parallelFilter.FilterByKeywords(filteredList, queryParams.Keywords);
                    stopwatch2.Stop();
                    Console.WriteLine($"Time spent filtering by keyword in memory: {stopwatch2.Elapsed}");
                }
                if (!queryParams.Ingredients.IsNullOrEmpty())
                {
                    //applying filtered on results that are allredy in memory
                    Stopwatch stopwatch3 = new Stopwatch { };
                    stopwatch3.Start();
                    filteredList = await parallelFilter.FilterByIngridents(filteredList, queryParams, true);
                    stopwatch3.Stop();
                    Console.WriteLine($"Time spent filtering by ingredient in memory: {stopwatch3.Elapsed}");
                }
                if (!queryParams.ExcludeIngredients.IsNullOrEmpty())
                {
                    Stopwatch stopwatch4 = new Stopwatch { };
                    stopwatch4.Start();
                    filteredList = parallelFilter.FilterByExcludedIngredients(filteredList, queryParams.ExcludeIngredients);
                    stopwatch4.Stop();
                    Console.WriteLine($"Time spent filtering by excluded in memory: {stopwatch4.Elapsed}");

                }

                List<Recipe> pagedList = filteredList.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                querryResult.TotalItems = filteredList.Count;
                querryResult.TotalPages = (int)Math.Ceiling((double)filteredList.Count / pageSize); ;
                querryResult.PageSize = totalItems;
                querryResult.Page = page;
                querryResult.Recipes = pagedList;




                var cacheEntryOptions = new MemoryCacheEntryOptions()
                   .SetSlidingExpiration(TimeSpan.FromMinutes(30));
                _cache.Set(cacheKey, filteredList, cacheEntryOptions);
                return (true, querryResult);
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occured during recipe filtering : {ex.Message}");
                throw;
            }

        }






        public (bool checkRquest, PagedQuerryResult result) GetRecipes(int page, int pageSize)
        {
            try
            {
                PagedQuerryResult querryResult = new PagedQuerryResult { };
                List<Recipe> result = new List<Recipe>();



                result = GlobalVariables.Recipes.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                querryResult.TotalItems = GlobalVariables.Recipes.Count();
                querryResult.Recipes = result;
                querryResult.TotalPages = GlobalVariables.Recipes.Count() / pageSize;
                querryResult.PageSize = pageSize;
                querryResult.Page = page;

                return (true, querryResult);
            }
            catch (Exception ex)
            {
                _logger.LogError($"an exception was cought while getting all recipes  : {ex.Message}");
                throw;
            }



        }
        public async Task<RecipeUpdateDto> UpdateRecipe(RecipeUpdateDto recipeUpdate)
        {
            try
            {
                var filter = Builders<Recipe>.Filter.Eq(dbrecipe => dbrecipe.RecipeId, recipeUpdate.RecipeId);
                var existingRecipe = await _recipesCollection.Find(filter).FirstOrDefaultAsync();
                if (existingRecipe == null)
                {
                    _logger.LogError($"Recipe with ID {recipeUpdate.RecipeId} not found.");
                    return null;
                }
                var existingRecipeClean = new RecipeUpdateDto(existingRecipe);
                var updateDefinition = new List<UpdateDefinition<Recipe>>();

                updateDefinition = await BuildUpdateDefinition(existingRecipe, recipeUpdate);

                // If there are no changes, return the existing recipe
                if (!updateDefinition.Any())
                {
                    _logger.LogInformation($"No changes detected for Recipe with ID {recipeUpdate.RecipeId}.");
                    return existingRecipeClean;
                }

                // Combine all update definitions and execute the update operation
                var combinedUpdate = Builders<Recipe>.Update.Combine(updateDefinition);
                await _recipesCollection.UpdateOneAsync(filter, combinedUpdate);
                var result = await GetRecipe(recipeUpdate.RecipeId);
                _logger.LogInformation($"Recipe with ID {recipeUpdate.RecipeId} updated successfully.");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError($"error during recipe update, exception: {ex.Message}");
                throw;
            }
        }
        private async Task<List<UpdateDefinition<Recipe>>> BuildUpdateDefinition(Recipe existingRecipe, RecipeUpdateDto recipeUpdate)
        {

            List<UpdateDefinition<Recipe>> updateDefinition = new List<UpdateDefinition<Recipe>>();
            UpdateIfChanged(updateDefinition, existingRecipe.Name, recipeUpdate.Name, r => r.Name);
            UpdateIfChanged(updateDefinition, existingRecipe.Keywords, recipeUpdate.Keywords, r => r.Keywords);
            UpdateIfChanged(updateDefinition, existingRecipe.RecipeCategory, recipeUpdate.RecipeCategory, r => r.RecipeCategory);
            UpdateIfChanged(updateDefinition, existingRecipe.ingredients_raw, recipeUpdate.ingredients_raw, r => r.ingredients_raw);
            UpdateIfChanged(updateDefinition, existingRecipe.RecipeIngredientParts, recipeUpdate.RecipeIngredientParts, r => r.RecipeIngredientParts);
            UpdateIfChanged(updateDefinition, existingRecipe.RecipeInstructions, recipeUpdate.RecipeInstructions, r => r.RecipeInstructions);
            UpdateIfChanged(updateDefinition, existingRecipe.RecipeServings, recipeUpdate.RecipeServings, r => r.RecipeServings);
            UpdateIfChanged(updateDefinition, existingRecipe.CookTime, recipeUpdate.CookTime ?? "", r => r.CookTime);
            UpdateIfChanged(updateDefinition, existingRecipe.RecipeYield, recipeUpdate.RecipeYield ?? "", r => r.RecipeYield);
            UpdateIfChanged(updateDefinition, existingRecipe.PrepTime, recipeUpdate.PrepTime ?? "", r => r.PrepTime);
            UpdateIfChanged(updateDefinition, existingRecipe.TotalTime, recipeUpdate.TotalTime ?? "", r => r.TotalTime);
            await UpdateNutritionalValue(updateDefinition, existingRecipe, recipeUpdate);


            return updateDefinition;
        }
        private void UpdateIfChanged<T>(List<UpdateDefinition<Recipe>> updateDefinition, T existingValue, T newValue, Expression<Func<Recipe, T>> field)
        {
            if (!EqualityComparer<T>.Default.Equals(existingValue, newValue))
            {
                updateDefinition.Add(Builders<Recipe>.Update.Set(field, newValue));
            }
        }
        private async Task<bool> UpdateNutritionalValue(List<UpdateDefinition<Recipe>> updateDefinition, Recipe existingRecipe, RecipeUpdateDto recipeUpdate)
        {
            var nutritionalFieldsMinMax = new Dictionary<string, (double min, double max)> { };
            var nutritionalFields = new Dictionary<string, (double existingValue, double newValue, double recipeServings)>
            {
                { "Calories", (existingRecipe.Calories, recipeUpdate.TotalCalories,recipeUpdate.RecipeServings) },
                { "FatContent", (existingRecipe.FatContent, recipeUpdate.TotalFatContent,recipeUpdate.RecipeServings) },
                { "SaturatedFatContent", (existingRecipe.SaturatedFatContent, recipeUpdate.TotalSaturatedFatContent,recipeUpdate.RecipeServings) },
                { "CholesterolContent", (existingRecipe.CholesterolContent, recipeUpdate.TotalCholesterolContent,recipeUpdate.RecipeServings) },
                { "SodiumContent", (existingRecipe.SodiumContent, recipeUpdate.TotalSodiumContent,recipeUpdate.RecipeServings) },
                { "CarbohydrateContent", (existingRecipe.CarbohydrateContent, recipeUpdate.TotalCarbohydrateContent,recipeUpdate.RecipeServings) },
                { "FiberContent", (existingRecipe.FiberContent, recipeUpdate.TotalFiberContent,recipeUpdate.RecipeServings) },
                { "SugarContent", (existingRecipe.SugarContent, recipeUpdate.TotalSugarContent,recipeUpdate.RecipeServings) },
                { "ProteinContent", (existingRecipe.ProteinContent, recipeUpdate.TotalProteinContent,recipeUpdate.RecipeServings) }
            };
            foreach (var field in nutritionalFields)
            {
                UpdateIfChanged(updateDefinition, field.Value.existingValue, field.Value.newValue, GetExpression<double>(field.Key));
                var maxValue = await GetMaxValueAsync(field.Key);

                var minValue = await GetMinValueAsync(field.Key);

                UpdateIfChanged(updateDefinition, GetMinMaxValue(existingRecipe, field.Key), getNormalizedValue(field.Value.newValue, maxValue, minValue, field.Value.recipeServings), GetExpression<double>($"{field.Key}_MinMax"));
            }
            return true;
        }
        private double GetMinMaxValue(Recipe recipe, string fieldName)
        {
            return (double)typeof(Recipe).GetProperty($"{fieldName}_MinMax").GetValue(recipe);
        }
        private Expression<Func<Recipe, double>> GetExpression<T>(string propertyName)
        {
            var parameter = Expression.Parameter(typeof(Recipe), "r");
            var property = Expression.Property(parameter, propertyName);
            return Expression.Lambda<Func<Recipe, double>>(property, parameter);
        }
        private async Task<double> GetMinValueAsync(string value)
        {
            var result = GlobalVariables.Recipes.Select(recipe =>
            {
                double devidedPrroperty = 0.0;
                var property = recipe.GetType().GetProperty(value);
                if (property != null)
                {
                    var propValue = (double)property.GetValue(recipe);
                    devidedPrroperty = propValue / recipe.RecipeServings;
                }
                return new { DevidedProperty = devidedPrroperty };
            })
            .Min(x => x.DevidedProperty);


            return result != null ? result : 0.0;
        }
        private async Task<double> GetMaxValueAsync(string value)
        {

            var result = GlobalVariables.Recipes.Select(recipe =>
            {
                double devidedPrroperty = 0.0;
                var property = recipe.GetType().GetProperty(value);
                if (property != null)
                {
                    var propValue = (double)property.GetValue(recipe);
                    devidedPrroperty = propValue / recipe.RecipeServings;
                }
                return new { DevidedProperty = devidedPrroperty };
            })
            .Max(x => x.DevidedProperty);


            return result != null ? result : 0.0;


        }

        public List<string> GetUniquePreferences()
        {
            try
            {
                
                var result= GlobalVariables.Recipes.SelectMany(recipe => recipe.Keywords) 
                .Distinct() 
                .ToList();
                return result;

            }
            catch (Exception)
            {

                throw;
            }
        }
        public List<string> GetUniqueIngredients()
        {
            try
            {

                var result = GlobalVariables.Recipes.SelectMany(recipe => recipe.RecipeIngredientParts)
                .Distinct()
                .ToList();
                return result;

            }
            catch (Exception)
            {

                throw;
            }
        }

        
    }


}


