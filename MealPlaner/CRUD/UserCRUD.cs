using MealPlaner.authentication;
using MealPlaner.CRUD.Interfaces;
using MealPlaner.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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
        /// <summary>
        /// Authenticates a user based on provided login credentials and returns a JWT token if authentication is successful.
        /// - **Credential Validation**: Checks for non-empty username and password fields and verifies the password against the stored hash.
        /// - **JWT Generation**: Generates a JWT token upon successful authentication, using the provided user information.
        /// - **Error Handling**: Logs any exceptions encountered and rethrows them for higher-level handling.
        /// </summary>
        /// <param name="user">An instance of <see cref="UserLogInDto"/> containing the username and password for login.</param>
        /// <returns>Returns a tuple <see cref="(bool Success, string Message, string jwt)"/> where:
        /// - **Success** is a <see cref="bool"/> indicating if the authentication was successful.
        /// - **Message** provides feedback on the outcome, including error messages if authentication fails.
        /// - **jwt** is a <see cref="string"/> representing the generated JWT token if authentication is successful; otherwise, null.</returns>
        /// <exception cref="Exception">Rethrows any exceptions encountered during authentication or JWT generation.</exception>
        
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

                if (checkdUser is null || !BCrypt.Net.BCrypt.Verify(user.Password, checkdUser.PasswordHash))
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

        /// <summary>
        /// Creates a new user with the specified details, including validation, password hashing, and duplicate username checking.
        /// - **Validation**: Ensures `Username`, `Password`, and `Email` are provided and that the username is unique.
        /// - **Password Security**: Hashes the user's password before storing it.
        /// - **Error Handling**: Logs any exceptions that occur during the process and returns a descriptive error message if the operation fails.
        /// </summary>
        /// <param name="user">An instance of <see cref="UserDto"/> containing the user's details for creation, such as username, password, and email.</param>
        /// <returns>Returns a tuple <see cref="(bool Success, string Message, UserResponseDto CreatedUser)"/> where:
        /// - **Success** is a <see cref="bool"/> indicating if the user was successfully created.
        /// - **Message** provides information on the outcome, including error messages if validation fails.
        /// - **CreatedUser** is a <see cref="UserResponseDto"/> representing the created user (excluding password), or null if creation failed.</returns>
        /// <exception cref="Exception">Logs and returns an error message if an exception is encountered during user creation.</exception>

        public async Task<(bool Success, string Message, UserResponseDto CreatedUser)> CreateUser(UserDto user)
        {
            if (user is null)
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
                var existingUsersEmail = await _usersCollection.Find(x => x.Email == user.Email).FirstOrDefaultAsync();

                if (existingUser != null)
                {
                    return (false, "Username already exists.", null);
                }
                if (existingUsersEmail != null) 
                {
                    return (false, "Check credentials.", null);
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

        /// <summary>
        /// Deletes a user by their unique ID from the database and returns the details of the deleted user if successful.
        /// This method retrieves the user by their ID before deletion to ensure the user exists. If the user is found, 
        /// they are deleted from the database, and their details are returned. If no user is found, null is returned.
        /// </summary>
        /// <param name="id">The unique identifier of the user to delete.</param>
        /// <returns>Returns a <see cref="UserResponseDto"/> containing the details of the deleted user if found and successfully deleted;
        /// otherwise, returns null if no user exists with the specified ID.</returns>
        /// <exception cref="Exception">Rethrows any exceptions encountered during the retrieval or deletion process.</exception>

        public async Task<UserResponseDto> DeleteUser(int id)
        {
            try
            {
                var deletedUser = await GetUser(id);
                if (deletedUser is null) 
                {
                    return null;
                }
                var filter = Builders<User>.Filter.Eq(r => r.UserId, id);
                await _usersCollection.DeleteOneAsync(filter);
                return deletedUser;
            }
            catch (Exception)
            {

                throw;
            }
        }
        /// <summary>
        /// Retrieves a user by their unique ID and returns their details in a response DTO if found.
        /// This method fetches a user from the database by their ID and, if found, maps the user data to a <see cref="UserResponseDto"/> 
        /// to provide the user's details in a structured format.
        /// </summary>
        /// <param name="id">The unique identifier of the user to retrieve.</param>
        /// <returns>Returns a <see cref="UserResponseDto"/> containing the user's details if found; otherwise, returns null if no user exists with the specified ID.</returns>
        /// <exception cref="Exception">Rethrows any exceptions encountered during the retrieval process.</exception>

        public async Task<UserResponseDto> GetUser(int id)
        {
            try
            {
                var user = await _usersCollection.Find(x => x.UserId == id).FirstOrDefaultAsync();
                if (user is null) {
                    return null;
                }
                UserResponseDto userResponse = new UserResponseDto(user);



                return userResponse;
            }
            catch (Exception)
            {

                throw;
            }
        }
        /// <summary>
        /// Retrieves a user by their email address and returns their details in a response DTO if found.
        /// This method fetches a user from the database by their email addres if found, maps the user data to a <see cref="UserResponseDto"/> 
        /// to provide the user's details in a structured format.
        /// </summary>
        /// <param name="email">The email address of the user to retrieve.</param>
        /// <returns>Returns a <see cref="UserResponseDto"/> containing the user's details if found; otherwise, returns null if no user exists with the specified email.</returns>
        /// <exception cref="Exception">Rethrows any exceptions encountered during the retrieval process.</exception>
        
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
        /// <summary>
        /// Retrieves a user by their username and returns their details in a response DTO if found
        /// This method fetches a user from the database by their username and, if found, maps the user data to a <see cref="UserResponseDto"/> 
        /// to provide the user's details in a structured format.
        /// </summary>
        /// <param name="username">The username of the user to retrieve.</param>
        /// <returns>Returns a <see cref="UserResponseDto"/> containing the user's details if found; otherwise, returns null if no user exists with the specified username.</returns>
        /// <exception cref="Exception">Rethrows any exceptions encountered during the retrieval process.</exception>

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
        /// <summary>
        /// Updates the details of an existing user based on the provided update data. Only modified fields are updated in the database.
        /// - **Change Detection**: Compares each field in `userUpdate` with the existing user data. Only fields with changes are updated.
        /// - **Logging**: Logs a message if the user does not exist or if there are no detected changes, indicating that no update was needed.
        /// - **Atomic Update**: Combines all updates into a single operation for efficiency and consistency in the database.
        /// </summary>
        /// <param name="userUpdate">An instance of <see cref="UserUpdateDto"/> containing the user's updated details.</param>
        /// <returns>Returns a <see cref="UserResponseDto"/> representing the updated user details if the update is successful, 
        /// or null if the user does not exist.</returns>
        /// <exception cref="Exception">Rethrows any exceptions encountered during the retrieval or update process.</exception>

        public async Task<UserResponseDto> UpdateUser(UserUpdateDto userUpdate)
        {
            try
            {
                var filter = Builders<User>.Filter.Eq(dbrecipe => dbrecipe.UserId, userUpdate.UserId);
                var existingUser= await _usersCollection.Find(filter).FirstOrDefaultAsync();
                if (existingUser is null)
                {
                    _logger.LogInformation($"Recipe with ID {userUpdate.UserId} not found.");
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
                .Limit(1) // Only retrieve  max id document
                .FirstOrDefaultAsync();


            return result != null ? result.UserId : 0;


        }
        /// <summary>
        /// Updates a user's list of previously used recipe IDs by appending new recipe IDs to the existing list.
        /// - **Appending Recipe IDs**: Adds each ID from `recipeIds` to the user's existing list of `PreviusRecipeIds`, ensuring any prior IDs are retained.
        /// - **Database Update**: Updates the `PreviusRecipeIds` field in the database with the new combined list.
        /// - **Null Check**: Verifies the user's existence before attempting updates. Returns null if the user is not found.
        /// </summary>
        /// <param name="userId">The unique identifier of the user whose recipe IDs are to be updated.</param>
        /// <param name="recipeIds">An array of recipe IDs to be added to the user's existing list of previous recipes.</param>
        /// <returns>Returns a <see cref="UserResponseDto"/> representing the updated user details, including the modified recipe IDs,
        /// or null if the user does not exist.</returns>
        /// <exception cref="Exception">Rethrows any exceptions encountered during the update process.</exception>

        public async Task<UserResponseDto> UpdateUserRecipeIds(int userId,int[] recipeIds)
        {
            var filter = Builders<User>.Filter.Eq(dbrecipe => dbrecipe.UserId, userId);
            User user= _usersCollection.Find(user => user.UserId == userId).FirstOrDefault();
            if (user is null) {
                return null;
            }
            List<int> prevRecipeIds = new List<int> { };

            if(!user.PreviusRecipeIds.IsNullOrEmpty())
            {
                prevRecipeIds = user.PreviusRecipeIds.ToList();
            }
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
