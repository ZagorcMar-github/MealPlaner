using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using System.Text.Json.Serialization;

namespace MealPlaner.Models
{
    public class MealCharacteristics
    {
        public List<string>? MustInclude { get; set; }
        public List<string>? MustExclude { get; set; }
        public double TargetCalorieProcent { get; set; }
        public double TargetFiberProcent { get; set; }
        public double TargetFatProcent { get; set; }
        public double TargetSaturatedFatProcent { get; set; }
        public double TargetSugarProcent { get; set; }
        public double TargetProteinProcent { get; set; }
        public double TargetCarbohydrateProcent { get; set; }
        public double TargetCholesterolProcent { get; set; }

    }
}
