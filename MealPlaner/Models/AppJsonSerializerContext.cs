using System.Text.Json.Serialization;

namespace MealPlaner.Models
{


    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UserCreateDto))]
    [JsonSerializable(typeof(UserLogInDto))]
    [JsonSerializable(typeof(UserResponseDto))]
    [JsonSerializable(typeof(Recipe))]
    [JsonSerializable(typeof(User))]
    [JsonSerializable(typeof(List<Recipe>))]
    [JsonSerializable(typeof(PagedQuerryResult))]

    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {
    }



}
