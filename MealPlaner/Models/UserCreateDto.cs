using System.ComponentModel.DataAnnotations;

namespace MealPlaner.Models
{
    public class UserCreateDto
    {
        public int UserId { get; set; }
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
        public string City { get; set; }
        public string Name { get; set; }
        public string Subscription { get; set; }
    }
}
