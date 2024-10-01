using MealPlaner.authentication;
using MealPlaner.CRUD.Interfaces;
using MealPlaner.Identity;
using MealPlaner.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.IdentityModel.Tokens.Jwt;


namespace MealPlaner.controllers
{
    
    [ApiController]
    //[ServiceFilter(typeof(APIKeyAuthFilter))]
    [Route("api/[controller]")]
    public class RecipeController : Controller
    {
        private IRecipeCRUD _recipeCRUD;

        private ILogger<RecipeController> _logger;
        public RecipeController(ILogger<RecipeController> logger, IRecipeCRUD recipeCRUD)
        {
            _recipeCRUD = recipeCRUD;
            _logger = logger;
        }
        [Authorize(Policy = CustomIdentityConstants.UserSubtierPolicyName)]
        [HttpGet("getRecipe/{id}")]
        public async Task<IActionResult> GetRecipe(int id)
        {

            try
            {

                Recipe result = await _recipeCRUD.GetRecipe(id);
                if (result ==null){
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
        public async Task<IActionResult> UpdateRecipe(Recipe recipe)
        {
            try
            {
                await _recipeCRUD.UpdateRecipe(recipe);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError("error");
                return BadRequest(ex);
            }
        }
        [HttpDelete("DeleteRecipe")]
        public async Task<IActionResult> DeleteRecipe(int id)
        {
            try
            {
                await _recipeCRUD.DeleteRecipe(id);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError("error");
                return BadRequest(ex);
            }
        }
        [HttpPost("CreateRecipe")]
        public async Task<IActionResult> CreateRecipe(Recipe recipe)
        {
            try
            {
                await _recipeCRUD.CreateRecipe(recipe);

                return Ok("recipe Inserted");
            }
            catch (Exception ex)
            {
                _logger.LogError("error");
                return BadRequest(ex);
            }
        }
        [Authorize]
        [EnableRateLimiting("bucketPerUser")]
        [HttpPost("generateMealPlan")]
        public async Task<IActionResult> GenerateMealPlan()
        {
            try
            {
                await _recipeCRUD.GenerateMealPlan();

                return Ok("rara");
            }
            catch (Exception ex)
            {
                _logger.LogError("error");
                return BadRequest(ex);
            }
        }
        [HttpGet("getRecipes")]
        public async Task<IActionResult> getRecipes([FromQuery]QueryParams querryParams,int page,int pageLimit )
        {
            try
            {
                (bool succes,PagedQuerryResult queryResult)= await _recipeCRUD.GetRecipes(querryParams, page,pageLimit);
                return Ok(queryResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest("internal server error");
                throw;
            }

        }

    }
}
