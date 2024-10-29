using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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
        [DefaultValue("free")]
        public string Subscription { get; set; }
        [DefaultValue("0")]
        public string Admin { get; set; }
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
            Admin = user.Admin;

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
