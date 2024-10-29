using MealPlaner.CRUD.Interfaces;
using MealPlaner.Identity;
using MealPlaner.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MealPlaner.controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class UserController : Controller
    {
        private IUserCRUD _userCRUD;

        private ILogger<UserController> _logger;
        public UserController(ILogger<UserController> logger, IUserCRUD userCRUD)
        {
            _userCRUD = userCRUD;
            _logger = logger;
        }

        [HttpGet("getUser/{id}")]
        public async Task<IActionResult> GetUser(int id )
        {

            try
            {
                var result= await _userCRUD.GetUser(id);
                if (result == null) {
                    return NotFound("user not found");
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(500,"somthing went wrong, contact support if the issue is not resolved");
            }

        }
        [HttpGet("getUserByUsername/{username}")]
        public async Task<IActionResult> GetUserByUsername(string username)
        {

            try
            {
                var result = await _userCRUD.GetUserByUsername(username);
                if (result == null)
                {
                    return NotFound("user not found");
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(500, "somthing went wrong, contact support if the issue is not resolved");
            }

        }
        [HttpGet("getUserByEmail/{email}")]
        public async Task<IActionResult> GetUserByEmail(string email)
        {

            try
            {
                var result = await _userCRUD.GetUserByEmail(email);
                if (result == null)
                {
                    return NotFound("user not found");
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(500, "somthing went wrong, contact support if the issue is not resolved");
            }

        }
        [HttpPut("UpdateUser")]
        public async Task<IActionResult> UpdateUser(UserUpdateDto user)
        {
            try
            {
                await _userCRUD.UpdateUser(user);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(500, "somthing went wrong, contact support if the issue is not resolved");
            }
        }
        [HttpPut("UpdateUserRecipeIds")]
        public async Task<IActionResult> UpdateUserRecipeIds(int userId, int[] recipeIds)
        {
            try
            {
               var response= await _userCRUD.UpdateUserRecipeIds(userId,recipeIds);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(500, "somthing went wrong, contact support if the issue is not resolved");
            }
        }
        [Authorize(CustomIdentityConstants.UserAdminPolicyName)]
        [HttpDelete("DeleteUser")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                
                var result =await _userCRUD.DeleteUser(id);
                if (result == null) 
                {
                    return BadRequest($"User with id {id} not found");
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(500, "somthing went wrong, contact support if the issue is not resolved");
            }
        }
        [HttpPost("CreateUser")]
        public async Task<IActionResult> CreateUser(UserDto userDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var (success, message, createdUser) = await _userCRUD.CreateUser(userDto);

                if (success)
                {
                    _logger.LogInformation("User created successfully: {Username}", createdUser.Username);
                    return CreatedAtAction(nameof(GetUser), new { id = createdUser.UserId }, createdUser);
                }
                else if (message == "Username already exists.")
                {
                    _logger.LogWarning("Attempt to create user with existing username: {Username}", userDto.Username);
                    return BadRequest(message);
                }
                else
                {
                    _logger.LogError("Failed to create user: {Message}", message);
                    return BadRequest(message);
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex.Message);
                return StatusCode(500, "somthing went wrong, contact support if the issue is not resolved");

            }
        }
        [HttpPost("AuthenticateUser")]
        public async Task<IActionResult> AuthenticateUser(UserLogInDto userDto)
        {
            try
            {
                var (success, message, test) = await _userCRUD.AuthenticateUser(userDto);
                if (success) {
                    return Ok(test);
                }
                return BadRequest(message);
                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(500, "somthing went wrong, contact support if the issue is not resolved");
            }
        }
    }
}
