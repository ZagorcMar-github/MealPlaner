namespace MealPlaner.Models
{
    public class UserResponseDto
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }

        public UserResponseDto(User user)
        {
            UserId = user.UserId;
            Username = user.Username;
            Email = user.Email;
        }
    }
}
