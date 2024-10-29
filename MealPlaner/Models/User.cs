using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using MongoDB.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace MealPlaner.Models
{
    [Collection("users")]
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? id { get; set; }
        public int UserId { get; set; }
        public int Age { get; set; }
        public double HeightCm { get; set; }
        public double WeightKg { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string Subscription { get; set; }
        public string Admin { get; set; } ="0";
        public int[]? PreviusRecipeIds { get; set; }



    }
   

    
}
