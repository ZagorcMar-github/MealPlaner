using MealPlaner.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Diagnostics;
using System.Text.RegularExpressions;
namespace MealPlaner.Services
{
    public class ParallelIngredientFilter
    {
        private readonly IMongoCollection<Recipe> _recipesCollection;

        public ParallelIngredientFilter()
        {
        }

        public ParallelIngredientFilter(IMongoCollection<Recipe> recipesCollection)
        {
            _recipesCollection = recipesCollection;
        }
        /// <summary>
        /// Filters recipes to exclude any that contain ingredients specified in the query. 
        /// Returns only recipes where none of the specified ingredients are present.
        /// Uses case-insensitive matching to compare ingredients in each recipe against the excluded ingredients in `queryIngredients`.
        /// </summary>
        /// <param name="recipes">A list of <see cref="Recipe"/> objects to filter.</param>
        /// <param name="queryIngredients">An array of ingredient names to exclude. Recipes containing any of these ingredients will be filtered out.</param>
        /// <returns>Returns a filtered <see cref="List{Recipe}"/> where none of the recipes contain the specified excluded ingredients.</returns>
        public List<Recipe> FilterByExcludedIngredients(List<Recipe> recipes, string[] queryIngredients) 
        {
            int matchCount = 0;
            var ingredientSet = new HashSet<string>(queryIngredients, StringComparer.OrdinalIgnoreCase);
            return recipes.Where(recipe => {
                var recipeIngredientSet = new HashSet<string>(recipe.RecipeIngredientParts, StringComparer.OrdinalIgnoreCase);
                matchCount = recipeIngredientSet.Intersect(ingredientSet, StringComparer.OrdinalIgnoreCase).Count();
                return (matchCount == 0);
            }).ToList();
            
        }
        /// <summary>
        /// Filters recipes to include only those that contain all the specified ingredients. 
        /// Returns recipes where each recipe's ingredients fully match the `queryIngredients`.
        /// Performs case-insensitive matching to ensure ingredients in each recipe match exactly with the provided `queryIngredients`.
        /// </summary>
        /// <param name="recipes">A list of <see cref="Recipe"/> objects to filter.</param>
        /// <param name="queryIngredients">An array of ingredient names that each recipe must include. Only recipes containing all these ingredients are returned.</param>
        /// <returns>Returns a filtered <see cref="List{Recipe}"/> containing only recipes that have all the specified ingredients.</returns>
        public List<Recipe> FilterByMustIncludeIngredients(List<Recipe> recipes, string[] queryIngredients)
        {
            int matchCount = 0;
            var ingredientSet = new HashSet<string>(queryIngredients, StringComparer.OrdinalIgnoreCase);
            return recipes.Where(recipe => {
                var recipeIngredientSet = new HashSet<string>(recipe.RecipeIngredientParts, StringComparer.OrdinalIgnoreCase);
                matchCount = recipeIngredientSet.Intersect(ingredientSet, StringComparer.OrdinalIgnoreCase).Count();
                //Console.WriteLine(matchCount);
                return (matchCount == queryIngredients.Count());
            }).ToList();

        }
        /// <summary>
        /// Filters recipes based on a list of required keywords, returning only those recipes where all specified keywords are present.
        /// Uses case-insensitive matching for keyword comparison. This method executes asynchronously to optimize performance.
        /// </summary>
        /// <param name="recipes">A list of <see cref="Recipe"/> objects to filter.</param>
        /// <param name="queryKeywords">An array of keywords to match. Only recipes containing all these keywords are returned.</param>
        /// <returns>Returns a filtered <see cref="List{Recipe}"/> containing recipes that match all specified keywords.</returns>
        public async Task<List<Recipe>> FilterByKeywords(List<Recipe> recipes, string[] queryKeywords)
        {
            return await Task.Run(() =>
            {
                int matchCount = 0;
                var ingredientSet = new HashSet<string>(queryKeywords, StringComparer.OrdinalIgnoreCase);
                return recipes.Where(recipe =>
                {
                    var recipeIngredientSet = new HashSet<string>(recipe.Keywords, StringComparer.OrdinalIgnoreCase);
                    matchCount = recipeIngredientSet.Intersect(ingredientSet, StringComparer.OrdinalIgnoreCase).Count();
                    return (matchCount == queryKeywords.Length);
                }).ToList();
            });

        }
        /// <summary>
        /// Filters recipes based on specified ingredients, using either fast in-memory matching or database-based filtering with regex,
        /// depending on the `fast` parameter. Supports matching based on a desired ingredient match percentage.
        /// - **Fast Filtering**: If `fast` is true, filtering is done in-memory using exact matches. 
        /// - **Database Filtering**: If `fast` is false, filters are applied in the database using regex, which may be slower but can handle large datasets more effectively.
        /// - **Ingredient Match Percentage**: Filters recipes based on the percentage of required ingredients specified in `DesiredIngredientMatchPercentage`.
        /// - **Performance Logging**: Logs the elapsed time for regex-based filtering to aid in performance monitoring and optimization.
        /// </summary>
        /// <param name="recipes">A list of <see cref="Recipe"/> objects to filter. This list is filtered in-memory if `fast` is true.</param>
        /// <param name="queryParams">An instance of <see cref="QueryParams"/> containing filtering criteria such as `Ingredients` and `DesiredIngredientMatchPercentage`.</param>
        /// <param name="fast">A boolean indicating the filtering method: if true, performs in-memory filtering; if false, uses database regex filtering.</param>
        /// <returns>Returns a <see cref="List{Recipe}"/> containing recipes that meet the specified ingredient criteria, based on the filtering method and match percentage.</returns>
        /// <exception cref="Exception">Catches and logs any exceptions encountered during database or in-memory filtering.</exception>

        public async Task<List<Recipe>> FilterByIngridents(List<Recipe> recipes, QueryParams queryParams, bool fast)
        {
            Console.WriteLine("____________________\nstarted filtering by ingredient");

            if (!fast)
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                var ingredientFilters = BuildIngredientFilters(queryParams.Ingredients);


                var filter = Builders<Recipe>.Filter.Or(ingredientFilters);


                recipes = await _recipesCollection.Find(filter)
                   .ToListAsync();
                stopwatch.Stop();

                Console.WriteLine($"Time elapesd filtering with regex: {stopwatch.Elapsed.TotalSeconds}");
            }

            var filteredRecipes = FilterRecipesByIngredientMatch(recipes, queryParams.Ingredients, fast, queryParams.DesiredIngredientMatchPercentage);
            Console.WriteLine("completed filtering by ingredient");
            return filteredRecipes;
        }
        private List<Recipe> FilterRecipesByIngredientMatch(List<Recipe> recipes, string[] queryIngredients,  bool fast, int desiredMatchPercentage = 55)
        {

            int matchCount = 0;
            var ingredientSet = new HashSet<string>(queryIngredients, StringComparer.OrdinalIgnoreCase);
            return recipes.Where(recipe =>
            {
                if (fast)
                {
                    var recipeIngredientSet = new HashSet<string>(recipe.RecipeIngredientParts, StringComparer.OrdinalIgnoreCase);
                    matchCount = recipeIngredientSet.Intersect(ingredientSet).Count();
                }
                else
                {

                    Dictionary<string, string> patterns = BuildIngredientPatterns(queryIngredients);
                    matchCount = ingredientSet.Count(ingredient =>
                       recipe.ingredients_raw.Any(recipeIngredient =>
                       Regex.IsMatch(recipeIngredient, patterns[ingredient], RegexOptions.IgnoreCase)));

                    //recipeIngredient.IndexOf(ingredient, StringComparison.OrdinalIgnoreCase) >= 0));
                }
                double matchPercentage = (double)matchCount / recipe.ingredients_raw.Count * 100;
                return matchPercentage >= desiredMatchPercentage;
            }).ToList();
        }

        

        private List<FilterDefinition<Recipe>> BuildIngredientFilters(string[] ingredients)
        {
            SpecialCases specialCases = new SpecialCases();
            var filters = new List<FilterDefinition<Recipe>>();
            var negativeFollowUps = new[] { "vinegar", "sauce", "paste", "powder", "juice", "oil", "syrup", "dressing", "cream", "butter", "flavor", "liqueur", "mix", "spread", "filling", "puree", "jam", "marmalade", "seed", "seeds", "starch", "stock", "broth" };
            var negLookahead = $"(?!\\s*(?:{string.Join("|", negativeFollowUps.Select(Regex.Escape))})\\b)";

            foreach (var ingredient in ingredients)
            {
                if (specialCases.SpecialCasePatterns.TryGetValue(ingredient.ToLower(), out var specialPatterns))
                {
                    filters.AddRange(specialPatterns.Select(pattern =>
                        Builders<Recipe>.Filter.Regex(x => x.ingredients_raw, new BsonRegularExpression(pattern, "i"))));
                }
                else
                {
                    var pattern = $"\\b{Regex.Escape(ingredient)}{negLookahead}";
                    filters.Add(Builders<Recipe>.Filter.Regex(x => x.ingredients_raw, new BsonRegularExpression(pattern, "i")));
                }
            }
            return filters;
        }
        private Dictionary<string, string> BuildIngredientPatterns(string[] ingredients)
        {
            var negativeFollowUps = new[] { "vinegar", "sauce", "paste", "powder", "juice", "oil", "syrup", "dressing", "cream", "butter", "flavor", "liqueur", "mix", "spread", "filling", "puree", "jam", "marmalade", "seed", "seeds", "starch", "stock", "broth" };
            SpecialCases specialCases = new SpecialCases();
            var patterns = new Dictionary<string, string>();
            var negLookahead = $"(?!\\s*(?:{string.Join("|", negativeFollowUps.Select(Regex.Escape))})\\b)";

            foreach (var ingredient in ingredients)
            {
                if (specialCases.SpecialCasePatterns.TryGetValue(ingredient.ToLower(), out var specialPatterns))
                {
                    patterns.Add(ingredient, specialPatterns[0]);
                }
                else
                {
                    var pattern = $"\\b{Regex.Escape(ingredient)}{negLookahead}";
                    patterns.Add(ingredient, pattern);
                }
            }

            return patterns;
        } public List<Recipe> GetIngredientFilteredRecipesFromDb(IMongoCollection<Recipe> collection, string[] ingredients, double minMatchPercentage = 55)
        {
            var queryableCollection = collection.AsQueryable();
            var query = queryableCollection
                .Select(recipe => new
                {
                    Recipe = recipe,
                    MatchingIngredients = recipe.RecipeIngredientParts.Intersect(ingredients).Count(),
                    TotalIngredients = recipe.ingredients_raw.Count
                })
                .Where(result =>
                (result.TotalIngredients == 0 && ingredients.Length == 0) ||
                (result.TotalIngredients > 0 &&
                    (double)result.MatchingIngredients / result.TotalIngredients * 100 >= minMatchPercentage))
                .Select(result => result.Recipe)
                .ToList();
            return query;

        }

        private List<Recipe> GetKeywordFilteredRecipesFromDb(IMongoCollection<Recipe> collection, string[] ingredients)
        {
            try
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                var querryableCollection = collection.AsQueryable();
                List<Recipe> recipes = new List<Recipe>();
                var keywordFilteredResult = querryableCollection.Select(recipe => new
                {
                    Recipe = recipe,
                    commonKeywordsCount = recipe.Keywords.Intersect(ingredients).Count(),
                    KeywordCount = ingredients.Length
                })
                    .Where(result => result.commonKeywordsCount == result.KeywordCount)
                    .Select(single => single.Recipe).ToList();
                recipes.AddRange(keywordFilteredResult); // doda vse elemente medtem ko add doda samo enga inherently
                stopWatch.Stop();
                TimeSpan ts2 = stopWatch.Elapsed;
                Console.WriteLine($"time spent filtering by Keyword with linq : {ts2.TotalSeconds}");
                return recipes;
            }
            catch (Exception ex) {
                Console.WriteLine(ex.Message);
                throw;
            }
        }




        public List<Recipe> GetMatchingRecipesUsingMongoAgregate(IMongoCollection<Recipe> collection, string[] ingredients, double minMatchPercentage = 55)
        {
            var ingredientBson = ingredients.Select(ingredient => (BsonValue)ingredient).ToList();
            var pipeline = new BsonDocument[]
            {
            new BsonDocument("$project", new BsonDocument
            {
                
                { "ingredientCount", new BsonDocument("$size", "$ingredients_raw") },
                { "matchedIngredients", new BsonDocument("$setIntersection", new BsonArray
                    {
                        "$RecipeIngredientParts",
                        new BsonArray(ingredientBson)
                    })
                },
                
            }),
            new BsonDocument("$project", new BsonDocument
            {

                { "ingredientCount", 1 },
                { "matchedIngredients",1},
                {"matchedCount",new BsonDocument("$size","$matchedIngredients") },
                { "condMatchedCount", new BsonDocument
                    {
                        { "$cond", new BsonDocument
                            {
                                { "if", new BsonDocument("$isArray", "$matchedIngredients") },
                                { "then",new BsonDocument("$size", "$matchedIngredients")},
                                { "else", 0 }
                            }
                        }
                    }
                },
                { "typeOfingredients_raw", new BsonDocument("$type", "$ingredients_raw")},




            }),
            new BsonDocument("$project", new BsonDocument
             {
                 { "matchPercentage",
                new BsonDocument("$divide", new BsonArray
                    {
                        new BsonDocument("$toDouble", "$matchedCount"),
                        new BsonDocument("$toDouble", "$ingredientCount")
                    })
                 },
                
                { "typeOfMatchCountarray", new BsonDocument("$type", "$matchedIngredients")},
                { "typeOfMatchCount", new BsonDocument("$type", "$matchedCount")},
                { "typeOfMatchCondCount", new BsonDocument("$type", "$condMatchedCount")},
                { "typeOfingredientCount", new BsonDocument("$type", "$ingredientCount")},
             }),
            
            /*,
            new BsonDocument("$match", new BsonDocument("matchPercentage", new BsonDocument("$gte", minMatchPercentage)))*/
            };
            var result = collection.Aggregate<BsonDocument>(pipeline).ToList();
            //result.ForEach(doc => Console.WriteLine(doc.ToJson()));


            var cursor = collection.Aggregate<BsonDocument>(pipeline);

            // Convert the cursor to a list of Recipe objects
            var matchingRecipes = new List<Recipe>();
            foreach (var document in cursor.ToEnumerable())
            {
                // Assuming Recipe has a constructor that accepts a BsonDocument
                // or manually map fields from BsonDocument to Recipe
                var recipe = BsonSerializer.Deserialize<Recipe>(document);
                matchingRecipes.Add(recipe);
            }

            return matchingRecipes;
        }
    }
}
