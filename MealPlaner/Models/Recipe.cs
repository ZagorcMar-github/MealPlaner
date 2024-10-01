using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace MealPlaner.Models
{
    [Collection("recipes")]
    public class Recipe
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }
        public int RecipeId { get; set; }
        public string Name { get; set; }
        public string CookTime { get; set; }
        public string PrepTime { get; set; }
        public string TotalTime { get; set; }   
        public string RecipeCategory { get; set; }
        public List<string> Keywords { get; set; }
        public List<string> RecipeIngredientParts { get; set; }
        public double Calories { get; set; }
        public double FatContent { get; set; }
        public double SaturatedFatContent { get; set; }
        public double CholesterolContent { get; set; }
        public double SodiumContent     { get; set; }
        public double CarbohydrateContent { get; set; }
        public double FiberContent { get; set; }
        public double SugarContent  { get; set; }
        public double ProteinContent { get; set; }
        public int RecipeServings { get; set; }
        public string RecipeYield { get; set; }
        public List<string> RecipeInstructions { get; set; }
        public List<string> ingredients_raw { get; set; }
        public double Calories_MinMax { get; set; }
        public double FatContent_MinMax { get;set; }
        public double SaturatedFatContent_MinMax { get; set; }
        public double SodiumContent_MinMax { get;set;}
        public double CholesterolContent_MinMax { get; set; }
        public double CarbohydrateContent_MinMax { get; set; }
        public double ProteinContent_MinMax { get; set; }
        public double SugarContent_MinMax { get; set; }
        public double FiberContent_MinMax { get; set; }




    }
}
