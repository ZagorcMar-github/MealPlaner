using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;

namespace MealPlaner.Models
{
    public class UserDto
    {
        public int Age { get; set; }
        [Required]
        [StringLength(50, MinimumLength = 3)]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; }
        public double HeightCm { get; set; }
        public double WeightKg { get; set; }
        public string Name { get; set; }
        public string Subscription { get; set; }
        public UserDto() { }
        
        public UserDto(User user) {

            Age = user.Age;
            Username = user.Username;
            Email = user.Email;
            Password =user.PasswordHash;
            HeightCm = user.HeightCm;
            WeightKg = user.WeightKg;
            Name = user.Name;
            Subscription = user.Subscription;

        }

    }
    public class UserUpdateDto : UserDto 
    {
        public int UserId { get; set; } 
        public int[] PreviusRecipeIds { get; set; }
        public UserUpdateDto() { }
        public UserUpdateDto(User user) : base(user) {

            UserId = user.UserId;
            PreviusRecipeIds = user.PreviusRecipeIds;
        }
    }

}
