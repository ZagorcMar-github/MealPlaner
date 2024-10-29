namespace MealPlaner.Models
{
    public class MealsDto
    {
        public string[]? Preferences { get; set; }
        public NutritionalGoals Goals { get; set; }
        public required Dictionary<string, MealCharacteristics> Meals { get; set; }
    }
}
