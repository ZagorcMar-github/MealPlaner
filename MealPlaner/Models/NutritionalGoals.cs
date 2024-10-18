namespace MealPlaner.Models
{
    public class NutritionalGoals
    {
        public double TargetCalories { get; set; } = 2000.0;//cal
        public double TargetFiberContent { get; set; } = 27.5;//g
        public double TargetFatContent { get; set; } = 33.0;//g
        public double TargetSaturatedFatContent { get; set; } = 22.0;//g
        public double TargetSugarContent { get; set; } = 25.0;//g
        public double TargetProteinContent { get; set; } = 48.0;//g
        public double TargetCarbohydrateContent { get; set; } = 135.0; //g
        public double TargetCholesterolContent { get; set; } = 300.0; //mg
    }
}
