using System.ComponentModel;

namespace MealPlaner.Models
{
    public class NutritionalGoals
    {
        [DefaultValue(2000.0)]
        public double TargetCalories { get; set; } = 2000.0;//cal
        [DefaultValue(27.5)]
        public double TargetFiberContent { get; set; } = 27.5;//g
        [DefaultValue(33.0)]
        public double TargetFatContent { get; set; } = 33.0;//g
        [DefaultValue(22.0)]
        public double TargetSaturatedFatContent { get; set; } = 22.0;//g
        [DefaultValue(25.0)]
        public double TargetSugarContent { get; set; } = 25.0;//g
        [DefaultValue(48.0)]
        public double TargetProteinContent { get; set; } = 48.0;//g
        [DefaultValue(135.0)]
        public double TargetCarbohydrateContent { get; set; } = 135.0; //g
        [DefaultValue(300.0)]
        public double TargetCholesterolContent { get; set; } = 300.0; //mg
    }
}
