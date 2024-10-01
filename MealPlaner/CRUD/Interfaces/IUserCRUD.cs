using MealPlaner.Models;

namespace MealPlaner.CRUD.Interfaces
{
    public interface IUserCRUD
    {
        public Task<(bool Success, string Message, User CreatedUser)> CreateUser(UserCreateDto user);
        public Task<User> UpdateUser(User user);
        public Task<User> DeleteUser(User user);
        public Task<User> GetUser(int id);
        public Task<User> GetUserByUsername(string username);
        public Task<User> GetUserByEmail(string email);
        public Task<(bool Success, string Message, string jwt)> AuthenticateUser(UserLogInDto user);    

    }
}
