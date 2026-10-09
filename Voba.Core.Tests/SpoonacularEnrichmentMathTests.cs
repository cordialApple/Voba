using System;
using System.Collections.Generic;
using Voba.Services;
using Xunit;

namespace Voba.Core.Tests
{
    // Exercises the cost/nutrition reduction extracted from the live Spoonacular call.
    // ComputeEnrichment works on a small projection of the SDK models, so the cents to
    // USD conversion, per-serving division, and case-insensitive macro summation can be
    // verified without hitting the network.
    public class SpoonacularEnrichmentMathTests
    {
        private static SpoonacularEnrichmentService.ParsedNutrient N(string name, decimal amount)
            => new(name, amount);

        [Fact]
        public void Totals_cost_and_scales_nutrition_per_serving()
        {
            var parsed = new List<SpoonacularEnrichmentService.ParsedIngredient>
            {
                new(250m, new[]
                {
                    N("Calories", 200m), N("Protein", 10m), N("Fat", 5m), N("Carbohydrates", 20m),
                }),
                // Mixed case + an unrelated nutrient that must be ignored.
                new(150m, new[]
                {
                    N("calories", 100m), N("protein", 5m), N("FAT", 3m),
                    N("carbohydrates", 10m), N("Sugar", 99m),
                }),
            };

            var result = SpoonacularEnrichmentService.ComputeEnrichment(parsed, servings: 2);

            Assert.NotNull(result);
            Assert.Equal(4.00m, result!.TotalCostUsd);     // (250 + 150) cents / 100
            Assert.Equal(2.00m, result.CostPerServingUsd); // 4.00 / 2
            Assert.Equal(150m, result.Nutrition.Calories); // (200 + 100) / 2
            Assert.Equal(7.5m, result.Nutrition.ProteinGrams);
            Assert.Equal(4.0m, result.Nutrition.FatGrams);
            Assert.Equal(15.0m, result.Nutrition.CarbGrams);
        }

        [Fact]
        public void Returns_null_when_no_cost_and_no_recognised_nutrition()
        {
            var parsed = new List<SpoonacularEnrichmentService.ParsedIngredient>
            {
                new(null, new[] { N("Sugar", 5m) }),
            };

            Assert.Null(SpoonacularEnrichmentService.ComputeEnrichment(parsed, 2));
        }

        [Fact]
        public void Returns_null_for_empty_list()
        {
            Assert.Null(SpoonacularEnrichmentService.ComputeEnrichment(
                new List<SpoonacularEnrichmentService.ParsedIngredient>(), 2));
        }

        [Fact]
        public void Cost_with_no_nutrition_still_returns_enrichment()
        {
            var parsed = new List<SpoonacularEnrichmentService.ParsedIngredient>
            {
                new(500m, Array.Empty<SpoonacularEnrichmentService.ParsedNutrient>()),
            };

            var result = SpoonacularEnrichmentService.ComputeEnrichment(parsed, 1);

            Assert.NotNull(result);
            Assert.Equal(5.00m, result!.TotalCostUsd);
            Assert.False(result.Nutrition.HasData);
        }
    }
}
