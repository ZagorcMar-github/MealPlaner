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

        //[Authorize(Policy = CustomIdentityConstants.UserSubtierPolicyName)]
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
                _logger.LogError("error");
                return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while processing your request.");
            }
        }
        [Authorize]
        [HttpDelete("DeleteRecipe")]
        public async Task<IActionResult> DeleteRecipe(int id)
        {
            try
            {
                var result= await _recipeCRUD.DeleteRecipe(id);
                _backgroundTaskQueue.QueueBackgroundWorkItem(async token =>
                {
                    await _inMemoryDataRefresh.ReloadData();
                });
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError("error");
                return BadRequest(ex);
            }
            finally { }
        }
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
                _logger.LogError("Error creating recipe: {Message}", ex.Message);
                return BadRequest(ex);
            }
        }
        [Authorize]
        [EnableRateLimiting("bucketPerUser")]
        [HttpPost("GenerateMealPlan")]
        public async Task<IActionResult> GenerateMealPlan([FromBody] DailyMealsDto meals)
        {
            try
            {
                var userHttpContext= HttpContext.Response.HttpContext;
                List<Recipe> result= await _recipeCRUD.GenerateMealPlan(userHttpContext,meals);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError("error");
                return BadRequest(ex);
            }
        }
        [HttpGet("getFilteredRecipes")]
        public async Task<IActionResult> GetFilteredRecipes([FromQuery] QueryParams querryParams, int page, int pageLimit)
        {
            try
            {
                (bool succes, PagedQuerryResult queryResult) = await _recipeCRUD.GetFilteredRecipes(querryParams, page, pageLimit);
                return Ok(queryResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest("internal server error");
                throw;
            }

        }
        [HttpGet("getRecipes")]
        public async Task<IActionResult> GetRecipes([FromQuery] int page, int pageLimit)
        {
            try
            {
                if (pageLimit > 100)
                {
                    return BadRequest("please enter a page limit under 100");
                }
                (bool succes, PagedQuerryResult queryResult) = _recipeCRUD.GetRecipes(page, pageLimit);
                if (succes)
                {
                    return Ok(queryResult);

                }
                else
                {
                    return BadRequest("recipes were not found");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest("internal server error");
                throw;
            }

        }
        [HttpGet("GetUniquePreferenceTypes")]
        public async Task<IActionResult> GetUniquePreferenceTypes() 
        {
            try
            {
                Stopwatch st = new Stopwatch();
                st.Start();
                var prefrences= _recipeCRUD.GetUniquePreferences();
                st.Stop();
                Console.WriteLine($"time spent getting keywords {st.Elapsed}");
                return Ok(prefrences);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }
        [HttpGet("GetUniqueIngredients")]
        public async Task<IActionResult> GetUniqueIngredients()
        {
            try
            {
                Stopwatch st = new Stopwatch();
                st.Start();
                var ingredients = _recipeCRUD.GetUniqueIngredients();
                st.Stop();
                Console.WriteLine($"time spent getting ingredients {st.Elapsed}");
                return Ok(ingredients);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }
        
    }
}
