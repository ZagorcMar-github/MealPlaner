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

        public List<Recipe> FilterByExcludedIngredients(List<Recipe> recipes, string[] queryIngredients) 
        {
            int matchCount = 0;
            var ingredientSet = new HashSet<string>(queryIngredients, StringComparer.OrdinalIgnoreCase);
            return recipes.Where(recipe => {
                var recipeIngredientSet = new HashSet<string>(recipe.RecipeIngredientParts, StringComparer.OrdinalIgnoreCase);
                matchCount = recipeIngredientSet.Intersect(ingredientSet, StringComparer.OrdinalIgnoreCase).Count();
                Console.WriteLine(matchCount);
                return (matchCount == 0);
            }).ToList();

        }
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
        public List<Recipe> FilterByKeywords(List<Recipe> recipes, string[] queryKeywords)
        {
            int matchCount = 0;
            var ingredientSet = new HashSet<string>(queryKeywords, StringComparer.OrdinalIgnoreCase);
            return recipes.Where(recipe => {
                var recipeIngredientSet = new HashSet<string>(recipe.Keywords, StringComparer.OrdinalIgnoreCase);
                matchCount = recipeIngredientSet.Intersect(ingredientSet, StringComparer.OrdinalIgnoreCase).Count();
                //Console.WriteLine($"match count:{matchCount} intersectCount: {queryKeywords.Count()}");
                return (matchCount == queryKeywords.Count());
            }).ToList();

        }
        public List<Recipe> GetRecipesThatInclude(List<Recipe> recipes, string[] ingredients)
        {
            int matchCount = 0;
            var ingredientSet = new HashSet<string>(ingredients, StringComparer.OrdinalIgnoreCase);
            return recipes.Where(recipe => {
                var recipeIngredientSet = new HashSet<string>(recipe.RecipeIngredientParts, StringComparer.OrdinalIgnoreCase);
                matchCount = recipeIngredientSet.Intersect(ingredientSet).Count();
                //Console.WriteLine($"match count:{matchCount} intersectCount: {queryKeywords.Count()}");
                return (matchCount == ingredients.Count());
            }).ToList();

        }
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

            var filteredRecipes = FilterRecipesByIngredientMatch(recipes, queryParams.Ingredients, fast);
            Console.WriteLine("completed filtering by ingredient");
            return filteredRecipes;
        }
        private List<Recipe> FilterRecipesByIngredientMatch(List<Recipe> recipes, string[] queryIngredients, bool fast)
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
                return matchPercentage >= 55;
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
