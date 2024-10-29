
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
        /// <summary>
        /// Initializes a MongoDB client and loads all recipes from the database into globally accessible storage for in-memory access.
        /// Typically used to preload data at the application's startup.
        /// - **Database Initialization**: Creates a MongoDB client and connects to the specified database and collection.
        /// - **Global Storage**: Stores the retrieved recipes in `GlobalVariables.Recipes` for efficient in-memory access throughout the application.
        /// - **Cancellation Support**: Supports cancellation during the data retrieval process.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the loading process if necessary.</param>
        /// <exception cref="Exception">Re-throws any exceptions encountered during MongoDB initialization or data retrieval.</exception>

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
