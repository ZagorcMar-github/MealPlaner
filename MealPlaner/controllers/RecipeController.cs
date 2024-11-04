using MealPlaner.authentication;
using MealPlaner.CRUD.Interfaces;
using MealPlaner.Identity;
using MealPlaner.Models;
using MealPlaner.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;


namespace MealPlaner.controllers
{

    [ApiController]
    //[ServiceFilter(typeof(APIKeyAuthFilter))]
    [Route("api/[controller]")]
    public class RecipeController : Controller
    {
        private IRecipeCRUD _recipeCRUD;
        private InMemoryDataRefresh _inMemoryDataRefresh;

        private ILogger<RecipeController> _logger;
        private IBackgroundTaskQueue _backgroundTaskQueue;

        public RecipeController(IRecipeCRUD recipeCRUD, InMemoryDataRefresh inMemoryDataRefresh, ILogger<RecipeController> logger, IBackgroundTaskQueue backgroundTaskQueue)
        {
            _recipeCRUD = recipeCRUD;
            _inMemoryDataRefresh = inMemoryDataRefresh;
            _logger = logger;
            _backgroundTaskQueue = backgroundTaskQueue;
        }
        /// <summary>
        /// Retrieves a recipe by its unique identifier.
        /// Returns detailed information about the specified recipe if it exists in the database.
        /// - **Example Usage**: `GET /getRecipe/{id}` where `id` is the recipe's unique identifier.
        /// - **Error Handling**: Returns HTTP 500 if an exception occurs during data retrieval.
        /// </summary>
        /// <param name="id">The unique identifier of the recipe to retrieve. Must correspond to an existing recipe ID.</param>
        /// <returns>Returns an <see cref="IActionResult"/> containing:
        /// - **200 OK** with the recipe details as a <see cref="RecipeDto"/> object if the recipe is found.
        /// - **404 Not Found** if no recipe is found with the specified <paramref name="id"/>.
        /// - **500 Internal Server Error** if an error occurs while processing the request.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the recipe with the specified <paramref name="id"/> does not exist.</exception>
        /// <exception cref="Exception">Catches any general exception and returns a server error status with a descriptive message.</exception>
        [HttpGet("getRecipe/{id}")]
        public async Task<IActionResult> GetRecipe(int id)
        {

            try
            {

                RecipeDto result = await _recipeCRUD.GetRecipe(id);
                if (result == null) {
                    return NotFound($"Recipe with id {id} not found.");
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while retrieving recipe with id {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }

        }
        /// <summary>
        /// Updates an existing recipe in the database with new details provided in the request.
        /// Triggers a background data refresh to ensure the updated recipe is reflected across in-memory data.
        /// - **Background Tasks**: After a successful update, triggers a background task to refresh in-memory data for consistency.
        /// - **Error Handling**: Returns HTTP 500 status if an exception occurs during update processing.
        /// - **Example Usage**: `PUT /UpdateRecipe` with JSON body conforming to the <see cref="RecipeUpdateDto"/> format.
        /// </summary>
        /// <param name="recipe">An instance of <see cref="RecipeUpdateDto"/> containing updated recipe information, 
        /// including the recipe ID to be updated and modified properties.</param>
        /// <returns>Returns an <see cref="IActionResult"/> containing:
        /// - **200 OK** with the updated recipe details if the update is successful.
        /// - **400 Bad Request** if no recipe exists with the specified ID in the <paramref name="recipe"/> parameter.
        /// - **500 Internal Server Error** if an error occurs while processing the update request.</returns>
        /// <exception cref="ArgumentNullException">Thrown if the input <paramref name="recipe"/> is null or missing required fields.</exception>
        /// <exception cref="Exception">Handles any general exceptions and logs them with an appropriate error message.</exception>

        [HttpPut("UpdateRecipe")]
        public async Task<IActionResult> UpdateRecipe(RecipeUpdateDto recipe)
        {
            try
            {
                var result= await _recipeCRUD.UpdateRecipe(recipe);
                if (result == null)
                {
                    return BadRequest("No recipe with such id");
                }
                _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
                {
                    await _inMemoryDataRefresh.ReloadData();
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"error updating recipe: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }
        /// <summary>
        /// Deletes a specified recipe by its unique identifier, accessible only by users with administrator privileges.
        /// If the recipe is successfully deleted, the method initiates a background task to refresh in-memory data 
        /// to ensure consistency across services. Returns details of the deleted recipe if successful, or a bad request 
        /// response if the recipe does not exist.
        /// </summary>
        /// <param name="id">The unique identifier of the recipe to delete. Must be a valid, existing recipe ID.</param>
        /// <returns>Returns an <see cref="IActionResult"/> containing:
        /// - **200 OK** with the details of the deleted recipe if deletion is successful.
        /// - **400 Bad Request** if no recipe exists with the specified <paramref name="id"/>.
        /// - **500 Internal Server Error** if an error occurs while processing the request.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown if the user does not have administrator privileges.</exception>
        /// <exception cref="KeyNotFoundException">Thrown if the recipe with the specified <paramref name="id"/> does not exist.</exception>

        [Authorize(CustomIdentityConstants.UserAdminPolicyName)]
        [HttpDelete("DeleteRecipe")]
        public async Task<IActionResult> DeleteRecipe(int id)
        {
            try
            {
                var result= await _recipeCRUD.DeleteRecipe(id);
                if (!result.found) 
                {
                    return BadRequest($"Recipe with id: {id} was not found");    
                }
                _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
                {
                    await _inMemoryDataRefresh.ReloadData();
                });
                return Ok(result.DeletedRecipe);
            }
            catch (Exception ex)
            {
                _logger.LogError($"error deleting Recipe: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }
        /// <summary>
        /// Creates one or more new recipes in the database based on the provided recipe data. 
        /// After successfully adding the recipes, triggers a background task to refresh in-memory data 
        /// to reflect the new entries across services.
        /// </summary>
        /// <param name="recipes">A list of <see cref="RecipeDto"/> objects representing the recipes to be created. 
        /// Each entry must contain the necessary details for a new recipe.</param>
        /// <returns>Returns an <see cref="IActionResult"/> containing:
        /// - **200 OK** with the details of the created recipes if successful.
        /// - **500 Internal Server Error** if an error occurs during the creation process.</returns>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="recipes"/> list is empty or contains invalid data.</exception>
        /// <exception cref="Exception">Catches any other general exceptions, returning a server error status with an appropriate message.</exception>

        [HttpPost("CreateRecipe")]
        public async Task<IActionResult> CreateRecipe([FromBody] List<RecipeDto> recipes)
        {
            try
            {
                var result = await _recipeCRUD.CreateRecipe(recipes);
                var response = Ok(result);


                _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
                {
                    await _inMemoryDataRefresh.ReloadData();
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating recipe: {ex.Message}" );
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }
        /// <summary>
        /// Generates an optimal meal plan tailored to user-defined nutritional goals, ingredient preferences, 
        /// and dietary restrictions. The plan takes a specified number of meals and proportionate 
        /// distribution of goals across those meals.
        /// - **Authorization**: This endpoint requires authorization.
        /// - **Rate Limiting**: Restricted by "bucketPerUser" rate-limiting policy.
        /// - **Error Handling**: Returns HTTP 500 with a descriptive error message if an error occurs during processing.
        /// - **Example Usage**: See `POST /GenerateMealPlan` with a JSON body conforming to <see cref="MealsDto"/> format.
        /// </summary>
        /// <param name="meals">An instance of <see cref="MealsDto"/> containing meal specifications, 
        /// including nutritional targets, ingredient inclusions/exclusions, meal count, and proportions for each meal.</param>
        /// <returns>Returns an <see cref="IActionResult"/> containing a list of <see cref="RecipeUpdateDto"/> objects 
        /// that represent the recommended recipes. If no suitable recipes are found, an empty list is returned.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown if the user is not authorized.</exception>
        /// <exception cref="ArgumentNullException">Thrown if the input <paramref name="meals"/> is null or missing required fields.</exception>

        [Authorize]
        [EnableRateLimiting("bucketPerUser")]
        [HttpPost("GenerateMealPlan")]
        public async Task<IActionResult> GenerateMealPlan([FromBody] MealsDto meals)
        {
            try
            {
                var userHttpContext= HttpContext.Response.HttpContext;
                List<RecipeUpdateDto> result= await _recipeCRUD.GenerateMealPlan(userHttpContext,meals);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"error generating meal plan: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }
        /// <summary>
        /// Retrieves a filtered list of recipes based on specified query parameters, with support for pagination.
        /// The filtered results are returned in a paged format according to the specified page number and page limit wich must be <100.
        /// </summary>
        /// <param name="querryParams">An instance of <see cref="QueryParams"/> containing filters for the recipes, such as 
        /// ingredients, dietary restrictions, cuisine types, or other criteria.</param>
        /// <param name="page">The page number for the paginated results. Must be a positive integer.</param>
        /// <param name="pageLimit">The maximum number of recipes to include per page. Must be a positive integer.</param>
        /// <returns>Returns an <see cref="IActionResult"/> containing:
        /// - **200 OK** with a <see cref="PagedQuerryResult"/> object containing the filtered recipes in paginated format if successful.
        /// - **500 Internal Server Error** if an error occurs during the filtering process.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the <paramref name="page"/> or <paramref name="pageLimit"/> parameters are invalid.</exception>
        /// <exception cref="Exception">Catches any general exceptions, returning a server error status with an appropriate message.</exception>

        [HttpPost("getFilteredRecipes")]
        public async Task<IActionResult> GetFilteredRecipes([FromBody] QueryParams querryParams, int page, int pageLimit)
        {
            try
            {
                (bool succes, PagedQuerryResult queryResult) = await _recipeCRUD.GetFilteredRecipes(querryParams, page, pageLimit);
                if (!succes) {
                    return BadRequest("page limit must be under 100");
                }
                return Ok(queryResult);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error filtering recipe: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }

        }
        /// <summary>
        /// Retrieves a list of recipes that match the specified name or a partial name. 
        /// Searches for recipes in the database that contain the provided name string.
        /// </summary>
        /// <param name="name">The name or partial name of the recipe(s) to search for. Must be a non-empty string.</param>
        /// <returns>Returns an <see cref="IActionResult"/> containing:
        /// - **200 OK** with a list of recipes that match the specified name.
        /// - **500 Internal Server Error** if an error occurs during the search process.</returns>
        /// <exception cref="ArgumentException">Thrown if the <paramref name="name"/> parameter is null or an empty string.</exception>
        /// <exception cref="Exception">Catches any general exceptions and returns a server error status with an appropriate message.</exception>

        [HttpGet("GetRecipesWithName")]
        public async Task<IActionResult> GetRecipesWithName(string name) 
        {
            try
            {
                var response = await _recipeCRUD.GetRecipeByName(name);
                return Ok(response);

            }
            catch (Exception ex)
            {
                _logger.LogError($"Error ");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }
        /// <summary>
        /// Retrieves a list of unique preference types available in the recipe database, such as dietary 
        /// restrictions, cuisine types, or other preference categories. Used for filtering and recommendation purposes.
        /// </summary>
        /// <returns>Returns an <see cref="IActionResult"/> containing:
        /// - **200 OK** with a list of unique preference types if the retrieval is successful.
        /// - **500 Internal Server Error** if an error occurs during the retrieval process.</returns>
        /// <exception cref="Exception">Handles any general exceptions that occur during execution, logging an appropriate error message 
        /// and returning a server error status.</exception>
        [HttpGet("GetUniquePreferenceTypes")]
        public async Task<IActionResult> GetUniquePreferenceTypes() 
        {
            try
            {
                Stopwatch st = new Stopwatch();
                st.Start();
                var prefrences= _recipeCRUD.GetUniquePreferences();
                st.Stop();
                Console.WriteLine($"Time spent getting keywords {st.Elapsed}");
                return Ok(prefrences);
            }
            catch (Exception ex)
            {
                _logger.LogError($"error getting preferences {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
                throw;
            }
        }
        /// <summary>
        /// Retrieves a list of unique ingredients from the recipe database, providing all distinct ingredients 
        /// available for filtering or search purposes.
        /// </summary>
        /// <returns>Returns an <see cref="IActionResult"/> containing:
        /// - **200 OK** with a list of unique ingredients if the retrieval is successful.
        /// - **500 Internal Server Error** if an error occurs during the retrieval process.</returns>
        /// <exception cref="Exception">Handles any general exceptions encountered during execution, logging an appropriate error message 
        /// and returning a server error status.</exception>
        [HttpGet("GetUniqueIngredients")]
        public async Task<IActionResult> GetUniqueIngredients()
        {
            try
            {
                Stopwatch st = new Stopwatch();
                st.Start();
                var ingredients = _recipeCRUD.GetUniqueIngredients();
                st.Stop();
                Console.WriteLine($"Time spent getting ingredients {st.Elapsed}");
                return Ok(ingredients);
            }
            catch (Exception ex)
            {
                _logger.LogError($"error getting getting unique ingredients {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request."); ;
                throw;
            }
        }
        
    }
}
