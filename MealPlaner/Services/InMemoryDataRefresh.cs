using MealPlaner.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Threading;

namespace MealPlaner.Services
{
    public class InMemoryDataRefresh
    {
        private readonly IMongoCollection<Recipe> _recipesCollection;
        private readonly ILogger<InMemoryDataRefresh> _logger;

        public InMemoryDataRefresh(IOptions<RecipesDatabaseSettings> recipesDatabaseSettings, ILogger<InMemoryDataRefresh> logger)
        {
            var mongoClient = new MongoClient(
            recipesDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                recipesDatabaseSettings.Value.DatabaseName);

            _recipesCollection = mongoDatabase.GetCollection<Recipe>(
                recipesDatabaseSettings.Value.RecipesCollectionName);
            _logger = logger;
        }

        public async Task ReloadData() 
        {
            try
            {
                Console.WriteLine("enetered thread");
                var recipes = await _recipesCollection.Find(Builders<Recipe>.Filter.Empty).ToListAsync();

                // Store them in some globally accessible storage (e.g., GlobalVariables)
                GlobalVariables.Recipes = recipes;
                Console.WriteLine("finished loading recipes");
                _logger.LogInformation("Recipes reloaded into memory successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to reload recipes into memory: {ex.Message}");
                throw;
            }
        }

    }
}
