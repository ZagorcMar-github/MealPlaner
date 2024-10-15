
using Amazon.Runtime.Internal.Util;
using MealPlaner.CRUD;
using MealPlaner.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace MealPlaner.Services
{
    public class LoadDataInMemoryOnStart : IHostedService
    {
        private readonly IMongoCollection<Recipe> _recipesCollection;
        private readonly ILogger<LoadDataInMemoryOnStart> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly RecipesDatabaseSettings _settings;

        public LoadDataInMemoryOnStart(IServiceProvider serviceProvider, IOptions<RecipesDatabaseSettings> settings) 
        {
            _serviceProvider = serviceProvider;
            _settings = settings.Value;

        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {


                var mongoClient = new MongoClient(_settings.ConnectionString);

                var mongoDatabase = mongoClient.GetDatabase(
                    _settings.DatabaseName);

                var _recipesCollection = mongoDatabase.GetCollection<Recipe>(
                    _settings.RecipesCollectionName);

                var value = await _recipesCollection.Find(Builders<Recipe>.Filter.Empty).ToListAsync(cancellationToken);

                GlobalVariables.Recipes = value;

            
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
