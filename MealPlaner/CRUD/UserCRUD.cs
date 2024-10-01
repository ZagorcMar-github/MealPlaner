using MealPlaner.authentication;
using MealPlaner.CRUD.Interfaces;
using MealPlaner.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
namespace MealPlaner.CRUD
{
    public class UserCRUD : IUserCRUD
    {
        private readonly IMongoCollection<User> _usersCollection;
        private readonly ILogger _logger;
        private readonly TokenService _tokenService;

        public UserCRUD(IOptions<RecipesDatabaseSettings> recipesDatabaseSettings, ILogger<UserCRUD> logger,TokenService tokenService)
        {

            var mongoClient = new MongoClient(
            recipesDatabaseSettings.Value.ConnectionString);

            var mongoDatabase = mongoClient.GetDatabase(
                recipesDatabaseSettings.Value.DatabaseName);

            _usersCollection = mongoDatabase.GetCollection<User>(
                recipesDatabaseSettings.Value.UsersCollectionName);
            _logger = logger;
            _tokenService = tokenService;
        }
        public async Task<(bool Success, string Message, string jwt)> AuthenticateUser(UserLogInDto user)
        {
            if (user == null)
            {
                return (false, "No user data", null);
            }
            try
            {
                if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.Password))
                {
                    return (false, "Username and password are required.", null);
                }
                User checkdUser = await _usersCollection.Find(x => x.Username == user.Username).FirstOrDefaultAsync();

                if (checkdUser == null)
                {
                    return (false, "Check credentials", null);
                }
                if (!BCrypt.Net.BCrypt.Verify(user.Password, checkdUser.PasswordHash))
                {
                    return (false, "Check credentials", null);
                }
                string jwtToken = _tokenService.GenerateToken(checkdUser);

                return (true, "User loged in successfully.", jwtToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }


        public async Task<(bool Success, string Message, User CreatedUser)> CreateUser(UserCreateDto user)
        {
            if (user == null)
            {
                return (false, "Invalid user data.", null);
            }

            try
            {
                // Validate user input
                if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.Password) || string.IsNullOrWhiteSpace(user.Email))
                {
                    return (false, "Username, password, and email are required.", null);
                }

                // Check if username already exists
                var existingUser = await _usersCollection.Find(x => x.Username == user.Username).FirstOrDefaultAsync();
                // var existingUsersEmail = await _usersCollection.Find(x => x.Email == user.Email).FirstOrDefaultAsync();
                if (existingUser != null)
                {
                    return (false, "Username already exists.", null);
                }

                // Hash the password
                string passwordHash = BCrypt.Net.BCrypt.HashPassword(user.Password);

                // Create a new user object with only necessary information
                var newUser = new User
                {
                    UserId = user.UserId, // Generate a new GUID for UserId
                    Username = user.Username,
                    City = user.City,
                    Subscription=user.Subscription,
                    Name = user.Name,
                    Age=user.Age,
                    Email = user.Email,
                    PasswordHash = passwordHash
                };

                // Insert the new user
                await _usersCollection.InsertOneAsync(newUser);


                // Return success with the created user (excluding password)
                var createdUser = new User
                {
                    UserId = newUser.UserId,
                    Username = newUser.Username,
                    Email = newUser.Email
                };

                return (true, "User created successfully.", createdUser);
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "Error occurred while creating user");
                return (false, "An error occurred while processing your request.", null);
            }
        }


        public async Task<User> DeleteUser(User user)
        {
            throw new NotImplementedException();
        }

        public async Task<User> GetUser(int id)
        {
            try
            {
                var user = await _usersCollection.Find(x => x.UserId == id).FirstOrDefaultAsync();
                return user;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<User> GetUserByEmail(string email)
        {
            throw new NotImplementedException();
        }

        public async Task<User> GetUserByUsername(string username)
        {
            throw new NotImplementedException();
        }

        public async Task<User> UpdateUser(User user)
        {
            throw new NotImplementedException();
        }
    }
}
