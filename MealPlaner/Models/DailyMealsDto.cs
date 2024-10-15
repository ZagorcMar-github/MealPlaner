namespace MealPlaner.Models
{
    public class DailyMealsDto
    {
        public string[]? Preferences { get; set; }
        public NutritionalGoals Goals { get; set; }
        public required Dictionary<string, DailyMealCharacteristics> DailyMeals { get; set; }
    }
}
