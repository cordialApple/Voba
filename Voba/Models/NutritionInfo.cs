namespace Voba.Models
{
    // Per-serving nutrition for a recipe, sourced from Spoonacular (or the offline fake).
    public class NutritionInfo
    {
        public decimal Calories { get; set; }
        public decimal ProteinGrams { get; set; }
        public decimal FatGrams { get; set; }
        public decimal CarbGrams { get; set; }

        public bool HasData =>
            Calories > 0 || ProteinGrams > 0 || FatGrams > 0 || CarbGrams > 0;
    }
}
