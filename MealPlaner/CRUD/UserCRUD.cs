using MealPlaner.authentication;
using MealPlaner.CRUD.Interfaces;
using MealPlaner.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;
namespace MealPlaner.CRUD
{
    public class UserCRUD : IUserCRUD
    {
        private readonly IMongoCollection<User> _usersCollection;
        private readonly ILogger _logger;
        private readonly TokenService _tokenService;

        public UserCRUD(IOptions<RecipesDatabaseSettings> recipesDatabaseSettings, ILogger<UserCRUD> logger, TokenService tokenService)
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


        public async Task<(bool Success, string Message, UserResponseDto CreatedUser)> CreateUser(UserDto user)
        {
            if (user == null)
            {
                return (false, "Invalid user data.", null);
            }

            try
            {
                var lastId = await GetLastIdAsync();
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

               
                var newUser = new User
                {
                    UserId = lastId+1, //get max value +1
                    Username = user.Username,
                    WeightKg = user.WeightKg,
                    HeightCm = user.HeightCm,
                    Subscription = user.Subscription,
                    Name = user.Name,
                    Age = user.Age,
                    Email = user.Email,
                    PasswordHash = passwordHash
                };

                // Insert the new user
                await _usersCollection.InsertOneAsync(newUser);


                // Return success with the created user (excluding password)
                var createdUser = new UserResponseDto(newUser);

                return (true, "User created successfully.", createdUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating user");
                return (false, "An error occurred while processing your request.", null);
            }
        }


        public async Task<UserResponseDto> DeleteUser(int id)
        {
            try
            {
                var deletedUser = await GetUser(id);
                var filter = Builders<User>.Filter.Eq(r => r.UserId, id);
                await _usersCollection.DeleteOneAsync(filter);
                return deletedUser;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<UserResponseDto> GetUser(int id)
        {
            try
            {
                var user = await _usersCollection.Find(x => x.UserId == id).FirstOrDefaultAsync();
                UserResponseDto userResponse = new UserResponseDto(user);



                return userResponse;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<UserResponseDto> GetUserByEmail(string email)
        {
            try
            {
                User user = await _usersCollection.Find(x => x.Email == email).FirstOrDefaultAsync();

                UserResponseDto userResponse = new UserResponseDto(user);


                return userResponse;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<UserResponseDto> GetUserByUsername(string username)
        {
            try
            {
                var user = await _usersCollection.Find(x => x.Username == username).FirstOrDefaultAsync();
                UserResponseDto userResponse = new UserResponseDto(user);


                return userResponse;
            }
            catch (Exception)
            {

                throw;
            }
        }

        public async Task<UserResponseDto> UpdateUser(UserUpdateDto userUpdate)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(dbrecipe => dbrecipe.UserId, userUpdate.UserId);
                var existingUser= await _usersCollection.Find(filter).FirstOrDefaultAsync();
                if (existingUser == null)
                {
                    _logger.LogError($"Recipe with ID {userUpdate.UserId} not found.");
                    return null;
                }
                var existingUserClean= new UserResponseDto(existingUser);
                var updateDefinition = new List<UpdateDefinition<User>>();

                updateDefinition = await BuildUpdateDefinition(existingUser, userUpdate);

                // If there are no changes, return the existing User
                if (!updateDefinition.Any())
                {
                    _logger.LogInformation($"No changes detected for User with ID {userUpdate.UserId}.");
                    return existingUserClean;
                }

                // Combine all update definitions and execute the update operation
                var combinedUpdate = Builders<User>.Update.Combine(updateDefinition);
                await _usersCollection.UpdateOneAsync(filter, combinedUpdate);
                return await GetUser(userUpdate.UserId);
            }
            catch (Exception)
            {

                throw;
            }
        }
        private void UpdateIfChanged<T>(List<UpdateDefinition<User>> updateDefinition, T existingValue, T newValue, Expression<Func<User, T>> field)
        {
            if (!EqualityComparer<T>.Default.Equals(existingValue, newValue))
            {
                updateDefinition.Add(Builders<User>.Update.Set(field, newValue));
            }
        }
        private async Task<List<UpdateDefinition<User>>> BuildUpdateDefinition(User existingUser, UserUpdateDto userUpdate)
        {
            List<UpdateDefinition<User>> updateDefinition = new List<UpdateDefinition<User>>();
            UpdateIfChanged(updateDefinition, existingUser.Username, userUpdate.Username, x => x.Username);
            UpdateIfChanged(updateDefinition, existingUser.Name, userUpdate.Name, x => x.Name);
            UpdateIfChanged(updateDefinition, existingUser.Age, userUpdate.Age, x => x.Age);
            UpdateIfChanged(updateDefinition, existingUser.WeightKg, userUpdate.WeightKg, x => x.WeightKg);
            UpdateIfChanged(updateDefinition, existingUser.HeightCm, userUpdate.HeightCm, x => x.HeightCm);
            UpdateIfChanged(updateDefinition, existingUser.Email, userUpdate.Email, x => x.Email);
            UpdateIfChanged(updateDefinition, existingUser.PreviusRecipeIds, userUpdate.PreviusRecipeIds, x => x.PreviusRecipeIds);
            return updateDefinition;

        }
        private async Task<int> GetLastIdAsync()
        {

            var result = await _usersCollection
                .Find(new BsonDocument())  // An empty filter to find all documents
                .Sort(Builders<User>.Sort.Descending("UserId")) // Sort by 'id' in descending order
                .Limit(1) // Only retrieve the first document (the one with the max id)
                .FirstOrDefaultAsync();


            return result != null ? result.UserId : 0;


        }

        public async Task<UserResponseDto> UpdateUserRecipeIds(int userId,int[] recipeIds)
        {
            var filter = Builders<User>.Filter.Eq(dbrecipe => dbrecipe.UserId, userId);
            User user= _usersCollection.Find(user => user.UserId == userId).FirstOrDefault();
            List<int> prevRecipeIds = user.PreviusRecipeIds.ToList();

            foreach (var item in recipeIds)
            {
                prevRecipeIds.Add(item);
            }
            UpdateDefinition<User> updateDefinition = Builders<User>.Update.Set(x => x.PreviusRecipeIds, prevRecipeIds.ToArray());
            await _usersCollection.UpdateOneAsync(filter, updateDefinition);

            return await GetUser(userId);
        }
    }
}
