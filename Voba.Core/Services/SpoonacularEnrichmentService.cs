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

            var nonempty = ingredients.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
            string ingredientList = string.Join("\n", nonempty);

            if (string.IsNullOrWhiteSpace(ingredientList))
                return null;

            var parsed = await _spoonacular.ParseIngredientsAsync(
                ingredientList, servings, includeNutrition: true);

            // Reduce the heavyweight SDK models to the only fields enrichment uses, so
            // the cost/nutrition arithmetic below can be exercised without the network.
            var mapped = parsed
                .Select(i => new ParsedIngredient(
                    i.EstimatedCost?.Value,
                    (i.Nutrition?.Nutrients ?? new())
                        .Select(n => new ParsedNutrient(n.Name, n.Amount, n.Unit))
                        .ToList(),
                    i.EstimatedCost?.Unit))
                .ToList();

            return ComputeEnrichment(mapped, servings, nonempty.Count);
        }

        // One parsed ingredient pared down to what enrichment needs: cost in US cents
        // (totalled across all servings) and its raw macro amounts.
        internal sealed record ParsedIngredient(
            decimal? CostCents,
            IReadOnlyList<ParsedNutrient> Nutrients,
            string? CostUnit = "US Cents");

        internal readonly record struct ParsedNutrient(string? Name, decimal Amount, string? Unit = null);

        // Pure cost + nutrition reduction. Split from the API call so the arithmetic
        // (cents to USD, per-serving division, macro summation) is unit-testable.
        internal static RecipeEnrichment? ComputeEnrichment(
            IReadOnlyList<ParsedIngredient> parsed, int servings, int? expectedIngredientCount = null)
        {
            if (servings <= 0) servings = 1;
            if (parsed.Count == 0) return null;

            // CostCents is in US cents and totalled across all servings.
            bool completePrices = parsed.Count == (expectedIngredientCount ?? parsed.Count) &&
                parsed.All(i => i.CostCents is >= 0 &&
                    string.Equals(i.CostUnit?.Trim(), "US Cents", StringComparison.OrdinalIgnoreCase));
            decimal totalCents = completePrices ? parsed.Sum(i => i.CostCents!.Value) : 0m;

            decimal totalUsd = totalCents > 0 ? Math.Round(totalCents / 100m, 2) : 0m;
            decimal perServingUsd = totalUsd > 0 ? Math.Round(totalUsd / servings, 2) : 0m;

            var nutrition = parsed.Count == (expectedIngredientCount ?? parsed.Count)
                ? SumNutrition(parsed, servings)
                : null;

            if (totalUsd == 0 && nutrition is null)
                return null;

            return new RecipeEnrichment(perServingUsd, totalUsd,
                nutrition ?? new NutritionInfo(), RecipeDataSource.Real);
        }

        // Totals the key macros across every parsed ingredient, then reduces to per-serving.
        private static NutritionInfo? SumNutrition(
            IReadOnlyList<ParsedIngredient> parsed, int servings)
        {
            var nutrition = new NutritionInfo();

            foreach (var ingredient in parsed)
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var nutrient in ingredient.Nutrients)
                {
                    if (nutrient.Amount < 0)
                        return null;
                    var unit = nutrient.Unit?.Trim().ToLowerInvariant();
                    var name = nutrient.Name?.Trim().ToLowerInvariant();
                    switch (name)
                    {
                        case "calories" when unit is "kcal":
                            nutrition.Calories += nutrient.Amount;
                            seen.Add(name);
                            break;
                        case "calories" when unit is "kj":
                            nutrition.Calories += nutrient.Amount / 4.184m;
                            seen.Add(name);
                            break;
                        case "protein":
                            if (!TryToGrams(nutrient.Amount, unit, out var protein)) return null;
                            nutrition.ProteinGrams += protein;
                            seen.Add(name);
                            break;
                        case "fat":
                            if (!TryToGrams(nutrient.Amount, unit, out var fat)) return null;
                            nutrition.FatGrams += fat;
                            seen.Add(name);
                            break;
                        case "carbohydrates":
                            if (!TryToGrams(nutrient.Amount, unit, out var carbs)) return null;
                            nutrition.CarbGrams += carbs;
                            seen.Add(name);
                            break;
                        case "calories":
                            return null;
                    }
                }
                if (seen.Count != 4)
                    return null;
            }

            nutrition.Calories     = Math.Round(nutrition.Calories / servings, 0);
            nutrition.ProteinGrams = Math.Round(nutrition.ProteinGrams / servings, 1);
            nutrition.FatGrams     = Math.Round(nutrition.FatGrams / servings, 1);
            nutrition.CarbGrams    = Math.Round(nutrition.CarbGrams / servings, 1);

            return nutrition;
        }

        private static bool TryToGrams(decimal amount, string? unit, out decimal grams)
        {
            grams = unit switch
            {
                "g" => amount,
                "mg" => amount / 1000m,
                "kg" => amount * 1000m,
                _ => 0m
            };
            return unit is "g" or "mg" or "kg";
        }
    }
}
