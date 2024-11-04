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
        /// <summary>
        /// Retrieves a user by their unique ID and returns their details if found.
        /// This method is used to fetch user information based on their ID. If the user does not exist, 
        /// a 404 status with an appropriate message is returned. Logs any exceptions that occur during the operation.
        /// </summary>
        /// <param name="id">The unique identifier of the user to retrieve.</param>
        /// <returns>Returns an <see cref="IActionResult"/> containing:
        /// - **200 OK** with the user details if the user is found.
        /// - **404 Not Found** if no user exists with the specified ID.
        /// - **500 Internal Server Error** if an error occurs during the retrieval process.
        /// - **429 unauthorized if somenone other than an admin trys to call the function</returns>
        /// <exception cref="Exception">Logs and returns a 500 status if an exception is encountered during user retrieval.</exception>

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
        /// <summary>
        /// Retrieves a user by their unique username and returns their details if found.
        /// This method allows fetching user information based on their username. 
        /// If the user does not exist, a 404 status with an appropriate message is returned. Logs any exceptions that occur during the operation.
        /// </summary>
        /// <param name="username">The unique username of the user to retrieve.</param>
        /// <returns>Returns an <see cref="IActionResult"/> containing:
        /// - **200 OK** with the user details if the user is found.
        /// - **404 Not Found** if no user exists with the specified username.
        /// - **500 Internal Server Error** if an error occurs during the retrieval process.
        /// - **429 unauthorized if somenone other than an admin trys to call the function</returns>
        /// <exception cref="Exception">Logs and returns a 500 status if an exception is encountered during user retrieval.</exception>
        [Authorize(CustomIdentityConstants.UserAdminPolicyName)]
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
        /// <summary>
        /// Retrieves a user by their email address and returns their details if found. 
        /// Access to this endpoint requires administrative authorization.
        
        /// This method allows administrators to fetch user information based on their email address. 
        /// If the user does not exist, a 404 status with an appropriate message is returned. Logs any exceptions that occur during the operation.
        
        /// </summary>
        /// <param name="email">The email address of the user to retrieve.</param>
        /// <returns>Returns an <see cref="IActionResult"/> containing:
        /// - **200 OK** with the user details if the user is found.
        /// - **404 Not Found** if no user exists with the specified email.
        /// - **500 Internal Server Error** if an error occurs during the retrieval process.</returns>
        /// <exception cref="Exception">Logs and returns a 500 status if an exception is encountered during user retrieval.</exception>

        [Authorize(CustomIdentityConstants.UserAdminPolicyName)]
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
        /// <summary>
        /// Updates the details of an existing user based on the provided user information.
        /// This method allows updating user details, such as name, email, or other relevant information. Logs any exceptions encountered during the operation.
        /// </summary>
        /// <param name="user">An instance of <see cref="UserUpdateDto"/> containing the updated details for the user.</param>
        /// <returns>Returns an <see cref="IActionResult"/> indicating:
        /// - **200 OK** if the update is successful.
        /// - **500 Internal Server Error** if an error occurs during the update process.</returns>
        /// <exception cref="Exception">Logs and returns a 500 status if an exception is encountered during user updating.</exception>

        [HttpPut("UpdateUser")]
        public async Task<IActionResult> UpdateUser(UserUpdateDto user)
        {
            try
            {
                var result= await _userCRUD.UpdateUser(user);
                if (result == null) 
                {
                    return BadRequest("user not found");
                }
                return Ok(result);
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
