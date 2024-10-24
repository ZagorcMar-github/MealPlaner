using MealPlaner.Models;

namespace MealPlaner.CRUD.Interfaces
{
    public interface IUserCRUD
    {
        public Task<(bool Success, string Message, UserResponseDto CreatedUser)> CreateUser(UserDto user);
        public Task<UserResponseDto> UpdateUser(UserUpdateDto user);
        public Task<UserResponseDto> DeleteUser(int id);
        public Task<UserResponseDto> GetUser(int id);
        public Task<UserResponseDto> GetUserByUsername(string username);
        public Task<UserResponseDto> GetUserByEmail(string email);
        public Task<UserResponseDto> UpdateUserRecipeIds(int userId,int[] recipeIds);
        public Task<(bool Success, string Message, string jwt)> AuthenticateUser(UserLogInDto user);    

    }
}
