using MealPlaner.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
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
        public async Task<List<Recipe>> FilterByIngredientsParallel(List<Recipe> keywordFilteredResult, QueryParams queryParams)
        {

            if (keywordFilteredResult == null || keywordFilteredResult.Count == 0)
            {
                return new List<Recipe>();
            }

            int processorCount = Environment.ProcessorCount;
            int chunkSize = (int)Math.Ceiling((double)keywordFilteredResult.Count / processorCount);
            var tasks = new List<Task<List<Recipe>>>();
            for (int i = 0; i < 6; i++)
            {
                var chunk = keywordFilteredResult
                    .Skip(i * chunkSize)
                    .Take(chunkSize)
                    .ToList();

                tasks.Add(Task.Run(() => FilterByIngridents(chunk, queryParams,true)));
            }
            var results = await Task.WhenAll(tasks);
            var filteredRecipes = results.SelectMany(x => x).ToList();

            Console.WriteLine("completed filtering by ingredient");
            return filteredRecipes;

        }
        public async Task<List<Recipe>> FilterByIngridents(List<Recipe> recipes, QueryParams queryParams,bool fast)
        {
            Console.WriteLine("____________________\nstarted filtering by ingredient");

            if (fast)
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
            var filteredRecipes = FilterRecipesByIngredientMatch(recipes, queryParams.Ingredients,fast);
            Console.WriteLine("completed filtering by ingredient");
            return filteredRecipes;
        }
        private List<Recipe> FilterRecipesByIngredientMatch(List<Recipe> recipes, string[] queryIngredients,bool fast)
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
                return matchPercentage >= 70;
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
        public Dictionary<string, string> BuildIngredientPatterns(string[] ingredients)
        {
            var negativeFollowUps = new[] { "vinegar", "sauce", "paste", "powder", "juice", "oil", "syrup", "dressing", "cream", "butter", "flavor", "liqueur", "mix", "spread", "filling", "puree", "jam", "marmalade", "seed", "seeds", "starch", "stock", "broth" };
            SpecialCases specialCases = new SpecialCases();
            var patterns = new Dictionary<string,string>();
            var negLookahead = $"(?!\\s*(?:{string.Join("|", negativeFollowUps.Select(Regex.Escape))})\\b)";

            foreach (var ingredient in ingredients)
            {
                if (specialCases.SpecialCasePatterns.TryGetValue(ingredient.ToLower(), out var specialPatterns))
                {
                    patterns.Add(ingredient,specialPatterns[0]);
                }
                else
                {
                    var pattern = $"\\b{Regex.Escape(ingredient)}{negLookahead}";
                    patterns.Add(ingredient, pattern);
                }
            }

            return patterns;
        }
    }
}
