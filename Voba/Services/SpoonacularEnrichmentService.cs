using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Voba.Interfaces;
using Voba.Models;
using Voba.Spoonacular;

namespace Voba.Services
{
    // Live enrichment provider: parses Gemma's ingredient list through Spoonacular's
    // ParseIngredients endpoint (one call per recipe) to obtain real cost and nutrition.
    public class SpoonacularEnrichmentService : IRecipeEnrichmentService
    {
        private readonly SpoonacularService _spoonacular;

        public SpoonacularEnrichmentService(SpoonacularService spoonacular)
        {
            _spoonacular = spoonacular;
        }

        public async Task<RecipeEnrichment?> EnrichAsync(IEnumerable<string> ingredients, int servings)
        {
            if (servings <= 0) servings = 1;

            string ingredientList = string.Join("\n",
                ingredients.Where(i => !string.IsNullOrWhiteSpace(i)));

            if (string.IsNullOrWhiteSpace(ingredientList))
                return null;

            var parsed = await _spoonacular.ParseIngredientsAsync(
                ingredientList, servings, includeNutrition: true);

            if (parsed.Count == 0)
                return null;

            // EstimatedCost.Value is in US cents and totalled across all servings.
            decimal totalCents = parsed
                .Where(i => i.EstimatedCost != null)
                .Sum(i => i.EstimatedCost!.Value);

            decimal totalUsd = totalCents > 0 ? Math.Round(totalCents / 100m, 2) : 0m;
            decimal perServingUsd = totalUsd > 0 ? Math.Round(totalUsd / servings, 2) : 0m;

            var nutrition = SumNutrition(parsed, servings);

            if (totalUsd == 0 && !nutrition.HasData)
                return null;

            return new RecipeEnrichment(perServingUsd, totalUsd, nutrition);
        }

        // Totals the key macros across every parsed ingredient, then reduces to per-serving.
        private static NutritionInfo SumNutrition(
            IEnumerable<spoonacular.Models.IngredientInformation> parsed, int servings)
        {
            var nutrition = new NutritionInfo();

            foreach (var ingredient in parsed)
            {
                var nutrients = ingredient.Nutrition?.Nutrients;
                if (nutrients == null) continue;

                foreach (var nutrient in nutrients)
                {
                    switch (nutrient.Name?.Trim().ToLowerInvariant())
                    {
                        case "calories":      nutrition.Calories     += nutrient.Amount; break;
                        case "protein":       nutrition.ProteinGrams += nutrient.Amount; break;
                        case "fat":           nutrition.FatGrams     += nutrient.Amount; break;
                        case "carbohydrates": nutrition.CarbGrams    += nutrient.Amount; break;
                    }
                }
            }

            nutrition.Calories     = Math.Round(nutrition.Calories / servings, 0);
            nutrition.ProteinGrams = Math.Round(nutrition.ProteinGrams / servings, 1);
            nutrition.FatGrams     = Math.Round(nutrition.FatGrams / servings, 1);
            nutrition.CarbGrams    = Math.Round(nutrition.CarbGrams / servings, 1);

            return nutrition;
        }
    }
}
