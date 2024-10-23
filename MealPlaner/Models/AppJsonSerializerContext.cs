using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using System.Text.Json.Serialization;

namespace MealPlaner.Models
{


    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UserDto))]
    [JsonSerializable(typeof(UserLogInDto))]
    [JsonSerializable(typeof(UserResponseDto))]
    [JsonSerializable(typeof(Recipe))]
    [JsonSerializable(typeof(User))]
    [JsonSerializable(typeof(List<Recipe>))]
    [JsonSerializable(typeof(PagedQuerryResult))]
    [JsonSerializable(typeof(RecipeDto))]
    [JsonSerializable(typeof(RecipeUpdateDto))]
    [JsonSerializable(typeof(ValidationProblemDetails))]
    [JsonSerializable(typeof(DailyMealCharacteristics))]
    [JsonSerializable(typeof(NutritionalGoals))]
    [JsonSerializable(typeof(DailyMealsDto))]
    [JsonSerializable(typeof(List<DailyMealCharacteristics>))]
    [JsonSerializable(typeof(UserUpdateDto))]

    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {
    }



}
