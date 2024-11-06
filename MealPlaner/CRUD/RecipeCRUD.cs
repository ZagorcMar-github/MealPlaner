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
        /// <summary>
        /// Generates an optimized meal plan based on user-defined nutritional goals, available ingredients, and specific meal preferences. 
        /// Utilizes previous user history to avoid recipe repetition and dynamically adjusts nutritional distribution across meals.
        /// - **User Authentication**: Extracts and verifies the user ID from the JWT token in the HTTP context.
        /// - **Ingredient Filtering**: Uses specified preferences and ingredients to filter recipes, removing any that have been recently used.
        /// - **Nutritional Goal Adjustment**: Iteratively updates nutritional goals as recipes are assigned to each meal, ensuring balance across the plan.
        /// - **Nutritional Proportions**: Distributes nutritional goals proportionally based on specified meal percentages, ensuring varied recipe selection for each meal of the day week.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/> object used to extract the user ID from the JWT token in the request header.</param>
        /// <param name="meals">An instance of <see cref="MealsDto"/> containing preferences, nutritional goals, 
        /// and meal specifications including must-include and must-exclude ingredients for each meal.</param>
        /// <returns>Returns a <see cref="List{RecipeUpdateDto}"/> representing an optimized list of recipes for each meal specified in the <paramref name="meals"/> parameter. 
        /// Each recipe is tailored to meet the user's nutritional targets and ingredient preferences.
        /// If the user ID is invalid, returns an empty list.</returns>
        /// <exception cref="ArgumentException">Thrown if required fields in the <paramref name="meals"/> parameter are missing or invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown if an error occurs while filtering or selecting optimal recipes.</exception>
        /// <exception cref="Exception">Handles any other general exceptions, rethrowing them for logging or further handling.</exception>

        public async Task<List<RecipeUpdateDto>> GenerateMealPlan(HttpContext httpContext,MealsDto meals)
        {
            try {
                int userId = 0;
                Int32.TryParse(_headerRequestDecoder.ExtractUserIdFromJwt(httpContext), out userId);
                if (userId <= 0) 
                {
                    return new List<RecipeUpdateDto>();
                }

                ParallelIngredientFilter filter = new ParallelIngredientFilter(_recipesCollection);
                int[] prev5UsedRecipes = new int[5];
                UserResponseDto user= await _userCRUD.GetUser(userId);
                if (!user.PreviusRecipeIds.IsNullOrEmpty())
                {
                     prev5UsedRecipes = user.PreviusRecipeIds.TakeLast(5).ToArray();
                }
                List<Recipe> baseRecipes = GlobalVariables.Recipes;
                List<RecipeUpdateDto>? optimalRecipes = new List<RecipeUpdateDto> { };
                //excluding the previus 5 used recipes form the db
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
                                                                // the formula for the desired pocentile value (currentProcentileValue/1- sum(procentileValuesUptoNow))

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
                    // get the amount of (raw, non procentile) nutrition a meal should consist of mesured in grams and mg
                    var MealNutritionalGoal = getRawNutritionalValue(meals.Goals, mealCharacteristics); 
                    await NormalizeNutritionalValues(MealNutritionalGoal); //based on min max values of nutrition in our db we normalize the goals
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    stopwatch.Start();
                    var foundRecipe = FindOptimalRecipe(mealRecipes, MealNutritionalGoal);
                    stopwatch.Stop();
                    Console.WriteLine($"time elapsed finding optimal recipe goals: {stopwatch.Elapsed}");
                    //
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
                        optimalRecipes.Add(new RecipeUpdateDto(foundRecipe));
                    }
                    else
                    {
                        //could add default values in case of no found recipe 
                        optimalRecipes.Add(new RecipeUpdateDto());
                    }


                }
                return optimalRecipes;
            }
            catch (Exception ex)
            {
                throw;
            }

        }
        /// <summary>
        /// Identifies the optimal recipe from a list of recipes based on the specified normalized nutritional goals.
        /// Calculates the Euclidean distance between each recipe's nutritional properties and the target nutritional goals,
        /// returning the recipe that best matches these goals.
        /// - **Nutritional Matching**: Calculates the Euclidean distance between each recipe’s nutritional values and the target goals,
        /// ranking recipes by proximity to these goals. This ensures that the recipe selected is nutritionally close to the target desired values.
        /// - **Random Selection from Top Matches**: From the top five closest matches, selects one recipe at random to introduce slight variation.
        /// </summary>
        /// <param name="recipes">A <see cref="List{Recipe}"/> containing candidate recipes to be evaluated against the nutritional goals.</param>
        /// <param name="normalizedNutritionalGoals">An instance of <see cref="NutritionalGoals"/> containing target nutritional values,
        /// which are normalized to align with each recipe's nutritional content.</param>
        /// <returns>Returns a <see cref="Recipe"/> object that has the closest match to the specified nutritional goals based on
        /// the Euclidean distance calculation. If no recipes are found, returns a default empty recipe.</returns>
        /// <exception cref="InvalidOperationException">Thrown if any property values on the recipe or nutritional goals are invalid or inaccessible.</exception>

        private Recipe FindOptimalRecipe(List<Recipe> recipes, NutritionalGoals normalizedNutritionalGoals)
        {
                var RecipeProperties = typeof(Recipe).GetProperties().Where(p => p.Name.EndsWith("_MinMax"));
                var normalizedNutritionalGoalsProperties = typeof(NutritionalGoals).GetProperties();
                var goalValues = normalizedNutritionalGoalsProperties.ToDictionary(
                p => p.Name,
                p => (double)p.GetValue(normalizedNutritionalGoals)        
                );
                var normalizedNutritionalGoalsPropertieNames = normalizedNutritionalGoalsProperties.ToDictionary(
                    p => p.Name.Replace("Target", "") + "_MinMax",
                    p => p.Name
                );
            var recs = recipes.Select(recipe =>
            {
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
                foreach (var (normalizedName,goalName) in normalizedNutritionalGoalsPropertieNames)
                {

                    if (propertyDictionary.TryGetValue(normalizedName, out object RecipeNutValue) && goalValues.TryGetValue(goalName, out var normTargetNutritionalValue))
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
        /// <summary>
        /// gets the raw nutritional value for each nutrient considering the goal and proportional destribution
        /// </summary>
        /// <param name="dailyGoal"></param>
        /// <param name="mealCharacteristics"></param>
        /// <returns></returns>
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
        /// <summary>
        /// Normalizes the nutritional values within a <see cref="NutritionalGoals"/> object by scaling each target nutritional property 
        /// to a range based on its minimum and maximum values across the dataset. This process ensures consistent comparison across recipes.
        /// </summary>
        /// <param name="rawMealNutritionalValues">An instance of <see cref="NutritionalGoals"/> containing the raw nutritional target values for a meal.</param>
        /// <returns>Returns a boolean value indicating whether the normalization process was successful.</returns>
        /// <remarks>
        /// - **Normalization Process**: For each nutritional property in `rawMealNutritionalValues`, retrieves the min and max values and normalizes the target values.
        /// - **Dynamic Property Access**: Uses reflection to access and modify properties, ensuring flexibility if new properties are added to `NutritionalGoals`.
        /// - **Performance Measurement**: Logs the time taken for normalization, including min and max value retrieval times, to help with performance optimization.
        /// </remarks>
        /// <exception cref="Exception">Catches and logs any exceptions encountered during normalization, returning false to indicate failure.</exception>
        private async Task<bool> NormalizeNutritionalValues(NutritionalGoals rawMealNutritionalValues)
        {
            try
            {
                Stopwatch stopwatchs = new Stopwatch { };
                stopwatchs.Start();
                var properties = typeof(NutritionalGoals).GetProperties();
                foreach (var item in properties)
                {
                    var propertyName = item.Name;

                    
                    if (propertyName.StartsWith("Target"))
                    {
                        propertyName = propertyName.Substring("Target".Length);
                    }
                    Stopwatch stopwatch = new Stopwatch { };
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
                stopwatchs.Stop();
                Console.WriteLine($"time spent normalizing nutritional values: {stopwatchs.Elapsed}");
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        /// <summary>
        /// Adjusts the target nutritional percentages for each meal in a daily meal plan to ensure that they proprtionaly return the same value taking the last goal result 
        /// example: breakfast procent calores = 0.5 calorieGoal = 100  breakfast value 50  lunch calorie procent: 0.1 it should result (if we take the calorie Goal) in 10 but we wan to use the caloriegoal-breakfastValue 
        /// the function scales the 0.1 value to a value that would result in 10 from 50
        /// This method iterates through each target nutritional percentage property in <see cref="MealCharacteristics"/>, adjusting 
        /// values such that each meal's percentage contribution is balanced  This ensures that the 
        /// nutritional goals for all meals in a day are distributed accurately and proportionally.
        /// </summary>
        /// <param name="mealsCharacteristics">A dictionary containing <see cref="MealCharacteristics"/> objects for each meal,
        /// where each entry represents a specific meal and its initial target nutritional percentages.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="mealsCharacteristics"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if target nutritional values are set incorrectly or are inaccessible.</exception>

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
            Console.WriteLine($"time spent  creating ranged values: {stopwatch.Elapsed}");
        }
        /// <summary>
        /// Creates new recipes in the database based on the provided recipe data, calculating and normalizing nutritional values for each recipe.
        /// Each recipe receives a unique ID and nutritional values are normalized based on existing dataset min and max values.
        /// </summary>
        /// <param name="recipesDto">A list of <see cref="RecipeDto"/> objects containing the details of each recipe to be created,
        /// including ingredients, instructions, servings, and nutritional content.</param>
        /// <returns>Returns a <see cref="List{RecipeUpdateDto}"/> containing the created recipes with normalized nutritional values and assigned IDs.</returns>
        /// <remarks>
        /// - **Normalization**: Each nutritional component (e.g., calories, fat, protein) is normalized based on the min and max values across the dataset, 
        /// allowing for consistent comparisons. The normalized values are calculated using per-serving values.
        /// - **Unique ID Assignment**: Generates unique IDs for each recipe, based on the maximum `RecipeId` value currently in the database.
        /// - **Batch Insertion**: Inserts all new recipes into the database in a single batch for optimized performance.
        /// </remarks>

        public async Task<List<RecipeUpdateDto>> CreateRecipe(List<RecipeDto> recipesDto)
        {
            try
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
                foreach (var recipeDto in recipesDto)
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
                    lastId++;

                }

                await _recipesCollection.InsertManyAsync(recipesToInsert);

                return recipesResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"error occured while creating recipe: {ex.Message}",ex);
                throw;
            }
        }
        /// <summary>
        /// Normalizes a nutritional value based on the maximum and minimum values within the dataset,
        /// adjusting for the number of servings in the recipe. This scaled value allows for comparison 
        /// across recipes with differing serving sizes and nutritional ranges.
        /// This method normalizes a nutritional value to a range between 0 and 1, adjusting for servings. 
        /// Normalization allows for comparison across different recipes, helping ensure that recipes are 
        /// evaluated consistently within a uniform scale.
        /// </summary>
        /// <param name="originalValue">The original nutritional value to be normalized, such as calories or fat content.</param>
        /// <param name="maxValue">The maximum value of the nutritional component in the dataset, used as the upper bound for normalization.</param>
        /// <param name="minValue">The minimum value of the nutritional component in the dataset, used as the lower bound for normalization.</param>
        /// <param name="recipeServings">The number of servings for the recipe, used to adjust the original value to a per-serving basis.</param>
        /// <returns>Returns a <see cref="double"/> representing the normalized nutritional value. If `maxValue` equals `minValue`, returns 0 to avoid division by zero.</returns>
        /// <exception cref="Exception">Re-throws any exceptions encountered, allowing for error handling at the calling level.</exception>

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

        /// <summary>
        /// Deletes a recipe from the database based on the specified recipe ID.
        /// Returns a tuple indicating whether the recipe was found and successfully deleted, along with 
        /// the details of the deleted recipe if applicable.
        /// This method first checks if the recipe exists by retrieving it based on the given ID. If found, 
        /// it proceeds to delete the recipe from the database and returns the recipe details; otherwise, it 
        /// returns `false` for the `found` flag and `null` for the deleted recipe.
        /// </summary>
        /// <param name="id">The unique identifier of the recipe to delete.</param>
        /// <returns>Returns a tuple <see cref="(bool found, RecipeUpdateDto DeletedRecipe)"/> where:
        /// - **found** is a <see cref="bool"/> indicating whether the recipe was found.
        /// - **DeletedRecipe** is a <see cref="RecipeUpdateDto"/> containing the details of the deleted recipe, or null if no recipe was found.</returns>
        /// <exception cref="Exception">Logs and rethrows any exceptions encountered during the deletion process.</exception>

        public async Task<( bool found,RecipeUpdateDto DeletedRecipe)> DeleteRecipe(int id)
        {
            try
            {

                var deletedRecipe = await GetRecipe(id);
                if (deletedRecipe == null) 
                {
                    return (false, null);
                }
                var filter = Builders<Recipe>.Filter.Eq(r => r.RecipeId, id);
                await _recipesCollection.DeleteOneAsync(filter);
                return (true,deletedRecipe);
            }
            catch (Exception ex)
            {
                _logger.LogError($"exception during deletion of recipe{id} : {ex.Message}",ex);
                throw;
            }

        }
        /// <summary>
        /// Retrieves a list of recipes that contain the specified name or partial name. 
        /// Returns each matched recipe as a `RecipeUpdateDto` for easy display or further processing.
        /// Searches for recipes that include the specified `recipeName` within their names, 
        /// making it useful for broad searches or finding similarly named recipes.
        /// </summary>
        /// <param name="recipeName">The name or partial name to search for within recipe names.</param>
        /// <returns>Returns a <see cref="List{RecipeUpdateDto}"/> containing recipes that match the specified name.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="recipeName"/> is null or empty.</exception>
        /// <exception cref="Exception">Catches any other general exceptions encountered during the retrieval process.</exception>

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
        /// <summary>
        /// Retrieves a recipe by its unique ID and returns it as a `RecipeUpdateDto` if found.
        /// This method is useful for retrieving a specific recipe by ID for display or editing purposes.
        /// Returns null if no recipe matches the given ID.
        /// </summary>
        /// <param name="id">The unique identifier of the recipe to retrieve.</param>
        /// <returns>Returns a <see cref="RecipeUpdateDto"/> containing the recipe details if found; otherwise, returns null.</returns>
        /// <exception cref="Exception">Logs and rethrows any exceptions encountered during the retrieval process.</exception>

        public async Task<RecipeUpdateDto> GetRecipe(int id)
        {
            try
            {
                var result = await _recipesCollection.Find(x => x.RecipeId == id).FirstOrDefaultAsync();
                if (result == null) {
                    return null;
                }
                RecipeUpdateDto cleanResult = new RecipeUpdateDto(result);
                 
                return cleanResult;

            }
            catch (Exception ex)
            {

                _logger.LogInformation(ex.Message);
                throw;
            }

        }

        /// <summary>
        /// Retrieves a filtered list of recipes based on specified query parameters, with support for caching and pagination.
        /// The filtering process applies keywords, ingredient inclusions, and exclusions, optimizing with parallel processing.
        /// - **Caching**: Results are cached with a composite key built from query parameters and page size, and a sliding expiration of 30 minutes.
        /// - **Filtering**: Applies keyword, ingredient, and exclusion filters sequentially, optimizing with in-memory parallel filtering to reduce I/O overhead.
        /// - **Performance Measurement**: Logs timing for each filtering step, providing insights into processing efficiency.
        /// </summary>
        /// <param name="queryParams">An instance of <see cref="QueryParams"/> containing filters such as keywords, ingredients to include, 
        /// and ingredients to exclude. Determines which recipes match the specified criteria.</param>
        /// <param name="page">The page number for pagination, starting from 1. Defaults to 1 if not specified.</param>
        /// <param name="pageSize">The number of recipes per page. Defaults to 10 if not specified, with a maximum limit of 100.</param>
        /// <returns>Returns a tuple <see cref="(bool checkRequest, PagedQuerryResult result)"/> where:
        /// - **checkRequest** is a <see cref="bool"/> indicating whether the request parameters were valid.
        /// - **result** is a <see cref="PagedQuerryResult"/> containing the filtered and paginated recipes, along with pagination metadata.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified `pageSize` exceeds 100.</exception>
        /// <exception cref="Exception">Logs and rethrows any general exceptions encountered during the filtering or caching process.</exception>

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
                    List<RecipeUpdateDto> cachedFilteredRecipesU = cachedFilteredRecipes.Select(x => new RecipeUpdateDto(x)).ToList();

                    PagedQuerryResult cachedFilteredResult = new PagedQuerryResult
                    {
                        Recipes = cachedFilteredRecipesU,
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
                List<RecipeUpdateDto> pagedListU = pagedList.Select(x=> new RecipeUpdateDto(x)).ToList();
                querryResult.TotalItems = filteredList.Count;
                querryResult.TotalPages = (int)Math.Ceiling((double)filteredList.Count / pageSize); ;
                querryResult.PageSize = totalItems;
                querryResult.Page = page;
                querryResult.Recipes = pagedListU;




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

        /// <summary>
        /// Updates an existing recipe in the database based on the provided `RecipeUpdateDto`.
        /// If changes are detected, the method updates only modified fields, ensuring efficient database operations.
        /// - **Logging**: Logs update success or failure messages, including when no changes are detected.
        /// - **Change Detection**: Only fields that have changed from their existing values are updated in the database.
        /// </summary>
        /// <param name="recipeUpdate">An instance of <see cref="RecipeUpdateDto"/> containing the updated values for the recipe.</param>
        /// <returns>Returns an updated <see cref="RecipeUpdateDto"/> object representing the modified recipe if the update is successful. 
        /// If the recipe is not found, returns null.</returns>
        /// <exception cref="Exception">Logs and rethrows any exceptions encountered during the update process.</exception>

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
        /// <summary>
        /// Builds a list of update definitions for fields in the recipe that have changed compared to the current values in the database.
        /// Used to dynamically create update operations based on modified fields only.
        /// This method iterates through each relevant property, adding an update definition for fields that differ from the database values.
        /// </summary>
        /// <param name="existingRecipe">The current recipe instance in the database, used to compare with the update values.</param>
        /// <param name="recipeUpdate">An instance of <see cref="RecipeUpdateDto"/> containing the desired updates.</param>
        /// <returns>Returns a <see cref="List{UpdateDefinition{Recipe}}"/> representing the update operations to be applied.</returns>



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
        /// <summary>
        /// Adds an update definition to the specified list if the new value differs from the existing value.
        /// Compares the current and new values and adds an update operation only if they are not equal.
        /// </summary>
        /// <typeparam name="T">The type of the field being updated.</typeparam>
        /// <param name="updateDefinition">The list of update definitions to which the new update operation is added if there is a change.</param>
        /// <param name="existingValue">The current value of the field in the database.</param>
        /// <param name="newValue">The new value provided in the update request.</param>
        /// <param name="field">An expression specifying the field in the `Recipe` document to update.</param>

        private void UpdateIfChanged<T>(List<UpdateDefinition<Recipe>> updateDefinition, T existingValue, T newValue, Expression<Func<Recipe, T>> field)
        {
            if (!EqualityComparer<T>.Default.Equals(existingValue, newValue))
            {
                updateDefinition.Add(Builders<Recipe>.Update.Set(field, newValue));
            }
        }
        /// <summary>
        /// Updates the nutritional values of a recipe if any changes are detected, including normalization of nutritional values.
        /// Each nutritional component is checked for changes, and both raw and normalized values are updated accordingly.
        /// - **Normalization**: Normalizes nutritional values based on min and max values in the dataset, allowing for consistent comparisons.
        /// - **Field Matching**: Ensures both raw and normalized values are updated where changes are detected.
        /// </summary>
        /// <param name="updateDefinition">The list of update definitions to which the new update operations are added.</param>
        /// <param name="existingRecipe">The current recipe instance in the database, used to compare with updated nutritional values.</param>
        /// <param name="recipeUpdate">An instance of <see cref="RecipeUpdateDto"/> containing the updated nutritional values.</param>
        /// <returns>Returns a boolean value indicating successful update preparation.</returns>
        /// <exception cref="Exception">Catches and rethrows exceptions encountered during normalization or update definition creation.</exception>

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
        /// <summary>
        /// Retrieves a distinct list of unique preferences (keywords) from all recipes in the global collection.
        /// This method consolidates keywords across recipes to provide a comprehensive list of available preferences.
        /// This method is useful for generating a list of preferences that can be used for filtering, searching, or categorizing recipes.
        /// </summary>
        /// <returns>Returns a <see cref="List{string}"/> containing unique keywords from all recipes.</returns>
        /// <exception cref="Exception">Catches and rethrows any exceptions encountered during the retrieval process.</exception>

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
        /// <summary>
        /// Retrieves a distinct list of unique ingredients from all recipes in the global collection.
        /// This method consolidates ingredients across recipes to provide a comprehensive list of available ingredients.
        /// Useful for generating a list of ingredients that can be used for filtering, searching, or ingredient-based categorization.
        /// </summary>
        /// <returns>Returns a <see cref="List{string}"/> containing unique ingredients from all recipes.</returns>
        /// <exception cref="Exception">Catches and rethrows any exceptions encountered during the retrieval process.</exception>

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


