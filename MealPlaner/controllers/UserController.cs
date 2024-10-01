using MealPlaner.CRUD.Interfaces;
using MealPlaner.Models;
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
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(500,"an error");
            }

        }
        [HttpPut("updateUser")]
        public async Task<IActionResult> UpdateUser(User user)
        {
            try
            {
                await _userCRUD.UpdateUser(user);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError("error");
                return BadRequest(ex);
            }
        }
        [HttpDelete("DeleteUser")]
        public async Task<IActionResult> DeleteUser(User user)
        {
            try
            {
                await _userCRUD.DeleteUser(user);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError("error");
                return BadRequest(ex);
            }
        }
        [HttpPost("CreateUser")]
        public async Task<IActionResult> CreateUser(UserCreateDto userDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (success, message, createdUser) = await _userCRUD.CreateUser(userDto);

            if (success)
            {
                _logger.LogInformation("User created successfully: {Username}", createdUser.Username);
                return CreatedAtAction(nameof(GetUser), new { id = createdUser.UserId }, new UserResponseDto(createdUser));
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
                _logger.LogError("error");
                return BadRequest("internal server error");
            }
        }
    }
}
