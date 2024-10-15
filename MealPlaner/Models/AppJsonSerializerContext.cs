using Microsoft.AspNetCore.Mvc;
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
    [JsonSerializable(typeof(RecipeDto))]
    [JsonSerializable(typeof(RecipeUpdateDto))]
    [JsonSerializable(typeof(ValidationProblemDetails))]
    [JsonSerializable(typeof(DailyMealCharacteristics))]
    [JsonSerializable(typeof(NutritionalGoals))]
    [JsonSerializable(typeof(DailyMealsDto))]
    [JsonSerializable(typeof(List<DailyMealCharacteristics>))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {
    }



}
