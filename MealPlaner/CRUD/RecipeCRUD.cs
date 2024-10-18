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


        private const string CacheKeyPrefix = "FilteredRecipes_";
        public RecipeCRUD(IOptions<RecipesDatabaseSettings> recipesDatabaseSettings, ILogger<RecipeCRUD> logger, IMemoryCache cache)
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

        public async Task<List<Recipe>> GenerateMealPlan(DailyMealsDto meals)
        {
            try
            {
                ParallelIngredientFilter filter = new ParallelIngredientFilter(_recipesCollection);
                List<Recipe> baseRecipes = GlobalVariables.Recipes;
                List<Recipe>? optimalRecipes = new List<Recipe> { };
                List<Recipe>? keywordFilteredRecipes = new List<Recipe> { };
                if (!meals.Preferences.IsNullOrEmpty())
                {
                    baseRecipes = filter.FilterByKeywords(baseRecipes, meals.Preferences);
                }

                CreateRangedProcentageValues(meals.DailyMeals);

                foreach (var (key, value) in meals.DailyMeals)
                {
                    var mealRecipes = baseRecipes;

                    if (!value.MustInclude.IsNullOrEmpty()) 
                    {
                        mealRecipes = filter.FilterByMustIncludeIngredients(mealRecipes, value.MustInclude.ToArray());
                    }
                    if (!value.MustExclude.IsNullOrEmpty()) 
                    {
                        mealRecipes = filter.FilterByExcludedIngredients(mealRecipes,value.MustExclude.ToArray());
                    }
                    var MealNutritionalGoal = getRawNutritionalValue(meals.Goals, value);
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    stopwatch.Start();
                    await NormalizeNutritionalValues(MealNutritionalGoal);
                    stopwatch.Stop();
                    Console.WriteLine($"time elapsed normalizing goals: {stopwatch.Elapsed}");
                    var foundRecipe = FindOptimalRecipe(mealRecipes, MealNutritionalGoal);
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
                    if (property.CanRead) // Ensure the property can be read
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
            }).OrderBy(x => x.EuclidianDistance).Take(20).ToList();
            var random = new Random();
            Recipe optimalRecipe = new Recipe { };
            if (!recs.IsNullOrEmpty()) 
            {
            optimalRecipe = recs[random.Next(recs.Count)].Recipe;
            }
            return optimalRecipe;

            // calculate euclidian 
            // sort by lowest distance 
            //get random from top 20
        }
        private NutritionalGoals getRawNutritionalValue(NutritionalGoals dailyGoal, DailyMealCharacteristics mealCharacteristics)
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
                var properties = typeof(NutritionalGoals).GetProperties();
                foreach (var item in properties)
                {
                    var propertyName = item.Name;

                    // Remove the 'Target' prefix if it exists
                    if (propertyName.StartsWith("Target"))
                    {
                        propertyName = propertyName.Substring("Target".Length);
                    }

                    // Assuming you want to use this property name to get some values asynchronously
                    stopwatch.Start();
                    var minValue = await GetMinValueAsync(propertyName);
                    stopwatch.Stop();
                    Console.WriteLine($"Time Elapsed getting min value {stopwatch.Elapsed}");
                    Stopwatch stopwatch1 = new Stopwatch();
                    stopwatch1.Start();
                    var maxValue = await GetMaxValueAsync(propertyName);
                    stopwatch1.Stop();
                    Console.WriteLine($" Time Elapsed getting max value: {stopwatch1.Elapsed}");
                    var orginalValue = (double)item.GetValue(rawMealNutritionalValues);
                    item.SetValue(rawMealNutritionalValues, getNormalizedValue(orginalValue, maxValue, minValue, 1.0));
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;

            }
        }

        private void CreateRangedProcentageValues(Dictionary<string, DailyMealCharacteristics> mealsCharacteristics)
        {
            int mealsADay = mealsCharacteristics.Count;
            int index = 0;
            foreach (var (key, value) in mealsCharacteristics)
            {
                var meal = value;
                double scaler = (index == mealsADay - 1) ? 1 : (index == 0) ? 1 : mealsADay - index;

                var properties = typeof(DailyMealCharacteristics).GetProperties()
                    .Where(p => p.Name.StartsWith("Target") && p.Name.EndsWith("Procent"));

                if (index == mealsADay - 1)
                {
                    foreach (var prop in properties)
                    {
                        prop.SetValue(meal, 1);
                    }
                }
                else
                {
                    foreach (var prop in properties)
                    {
                        double currentValue = (double)prop.GetValue(meal);
                        prop.SetValue(meal, currentValue * scaler);
                    }
                }
                index++;
            }


        }

        public async Task<RecipeUpdateDto> CreateRecipe(RecipeDto recipeDto)
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


            await _recipesCollection.InsertOneAsync(recipe);
            var cleanRecipe = new RecipeUpdateDto(recipe);

            return cleanRecipe;
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

        public async Task<RecipeUpdateDto> GetRecipe(int id)
        {
            try
            {
                var result = await _recipesCollection.Find(x => x.RecipeId == id).FirstOrDefaultAsync(); ;
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
                    filteredList = parallelFilter.FilterByKeywords(filteredList, queryParams.Keywords);
                    stopwatch2.Stop();
                    Console.WriteLine($"Time spent filtering by ingredient in memory: {stopwatch2.Elapsed}");
                }
                if (!queryParams.Ingredients.IsNullOrEmpty())
                {
                    //applying filtered on results that are allredy in memory
                    Stopwatch stopwatch2 = new Stopwatch { };
                    stopwatch2.Start();
                    filteredList = await parallelFilter.FilterByIngridents(filteredList, queryParams, true);
                    stopwatch2.Stop();
                    Console.WriteLine($"Time spent filtering by ingredient in memory: {stopwatch2.Elapsed}");
                }
                if (!queryParams.ExcludeIngredients.IsNullOrEmpty())
                {
                    Stopwatch stopwatch3 = new Stopwatch { };
                    stopwatch3.Start();
                    filteredList = parallelFilter.FilterByExcludedIngredients(filteredList, queryParams.ExcludeIngredients);
                    stopwatch3.Stop();
                    Console.WriteLine($"Time spent filtering by excluded in memory: {stopwatch3.Elapsed}");

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




    }
    /*
     if all else fails 

                if (existingRecipe.Name != recipeUpdate.Name)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.Name, recipeUpdate.Name));

            if (!existingRecipe.Keywords.SequenceEqual(recipeUpdate.Keywords))
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.Keywords, recipeUpdate.Keywords));

            if (existingRecipe.RecipeCategory != recipeUpdate.RecipeCategory)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.RecipeCategory, recipeUpdate.RecipeCategory));

            if (!existingRecipe.ingredients_raw.SequenceEqual(recipeUpdate.ingredients_raw))
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.ingredients_raw, recipeUpdate.ingredients_raw));

            if (!existingRecipe.RecipeIngredientParts.SequenceEqual(recipeUpdate.RecipeIngredientParts))
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.RecipeIngredientParts, recipeUpdate.RecipeIngredientParts));

            if (!existingRecipe.RecipeInstructions.SequenceEqual(recipeUpdate.RecipeInstructions))
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.RecipeInstructions, recipeUpdate.RecipeInstructions));

            if (existingRecipe.RecipeServings != recipeUpdate.RecipeServings)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.RecipeServings, recipeUpdate.RecipeServings));

            if (existingRecipe.CookTime != recipeUpdate.CookTime)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.CookTime, recipeUpdate.CookTime ?? ""));

            if (existingRecipe.RecipeYield != recipeUpdate.RecipeYield)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.RecipeYield, recipeUpdate.RecipeYield ?? ""));

            if (existingRecipe.PrepTime != recipeUpdate.PrepTime)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.PrepTime, recipeUpdate.PrepTime ?? ""));

            if (existingRecipe.TotalTime != recipeUpdate.TotalTime)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.TotalTime, recipeUpdate.TotalTime ?? ""));

            // Nutritional values comparison
            if (existingRecipe.Calories != recipeUpdate.TotalCalories)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.Calories, recipeUpdate.TotalCalories));

            if (existingRecipe.FatContent != recipeUpdate.TotalFatContent)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.FatContent, recipeUpdate.TotalFatContent));

            if (existingRecipe.SaturatedFatContent != recipeUpdate.TotalSaturatedFatContent)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.SaturatedFatContent, recipeUpdate.TotalSaturatedFatContent));

            if (existingRecipe.CholesterolContent != recipeUpdate.TotalCholesterolContent)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.CholesterolContent, recipeUpdate.TotalCholesterolContent));

            if (existingRecipe.SodiumContent != recipeUpdate.TotalSodiumContent)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.SodiumContent, recipeUpdate.TotalSodiumContent));

            if (existingRecipe.CarbohydrateContent != recipeUpdate.TotalCarbohydrateContent)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.CarbohydrateContent, recipeUpdate.TotalCarbohydrateContent));

            if (existingRecipe.FiberContent != recipeUpdate.TotalFiberContent)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.FiberContent, recipeUpdate.TotalFiberContent));

            if (existingRecipe.SugarContent != recipeUpdate.TotalSugarContent)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.SugarContent, recipeUpdate.TotalSugarContent));

            if (existingRecipe.ProteinContent != recipeUpdate.TotalProteinContent)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.ProteinContent, recipeUpdate.TotalProteinContent));

            // Min-Max normalized values
            var caloriesMinMax = getNormalizedValue(recipeUpdate.TotalCalories, CaloriesMaxValue, CaloriesMinValue);
            if (existingRecipe.Calories_MinMax != caloriesMinMax)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.Calories_MinMax, caloriesMinMax));

            var fatContentMinMax = getNormalizedValue(recipeUpdate.TotalFatContent, FatContentMaxValue, FatContentMinValue);
            if (existingRecipe.FatContent_MinMax != fatContentMinMax)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.FatContent_MinMax, fatContentMinMax));

            var saturatedFatContentMinMax = getNormalizedValue(recipeUpdate.TotalSaturatedFatContent, SaturatedFatContentMaxValue, SaturatedFatContentMinValue);
            if (existingRecipe.SaturatedFatContent_MinMax != saturatedFatContentMinMax)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.SaturatedFatContent_MinMax, saturatedFatContentMinMax));

            var cholesterolContentMinMax = getNormalizedValue(recipeUpdate.TotalCholesterolContent, CholesterolContentMaxValue, CholesterolContentMinValue);
            if (existingRecipe.CholesterolContent_MinMax != cholesterolContentMinMax)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.CholesterolContent_MinMax, cholesterolContentMinMax));

            var sodiumContentMinMax = getNormalizedValue(recipeUpdate.TotalSodiumContent, SodiumContentMaxValue, SodiumContentMinValue);
            if (existingRecipe.SodiumContent_MinMax != sodiumContentMinMax)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.SodiumContent_MinMax, sodiumContentMinMax));

            var carbohydrateContentMinMax = getNormalizedValue(recipeUpdate.TotalCarbohydrateContent, CarbohydrateContentMaxValue, CarbohydrateContentMinValue);
            if (existingRecipe.CarbohydrateContent_MinMax != carbohydrateContentMinMax)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.CarbohydrateContent_MinMax, carbohydrateContentMinMax));

            var fiberContentMinMax = getNormalizedValue(recipeUpdate.TotalFiberContent, FiberContentMaxValue, FiberContentMinValue);
            if (existingRecipe.FiberContent_MinMax != fiberContentMinMax)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.FiberContent_MinMax, fiberContentMinMax));

            var sugarContentMinMax = getNormalizedValue(recipeUpdate.TotalSugarContent, SugarContentMaxValue, SugarContentMinValue);
            if (existingRecipe.SugarContent_MinMax != sugarContentMinMax)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.SugarContent_MinMax, sugarContentMinMax));

            var proteinContentMinMax = getNormalizedValue(recipeUpdate.TotalProteinContent, ProteinContentMaxValue, ProteinContentMinValue);
            if (existingRecipe.ProteinContent_MinMax != proteinContentMinMax)
                updateDefinition.Add(Builders<Recipe>.Update.Set(r => r.ProteinContent_MinMax, proteinContentMinMax));
            
     
     */

}


