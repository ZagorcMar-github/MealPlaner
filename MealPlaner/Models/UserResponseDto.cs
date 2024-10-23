namespace MealPlaner.Models
{
    public class UserResponseDto
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public int Age { get; set; }
        public double WeightKg { get; set; }
        public double HeightKg { get; set; }
        public int[] PreviusRecipeIds { get; set; }


        public UserResponseDto(User user)
        {
            UserId = user.UserId;
            Username = user.Username;
            Email = user.Email;
            Age = user.Age;
            WeightKg = user.WeightKg;
            HeightKg= user.HeightCm;
            PreviusRecipeIds = user.PreviusRecipeIds;
        }
    }
}
