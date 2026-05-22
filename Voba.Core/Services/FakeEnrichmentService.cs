using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Services
{
    // Offline stand-in for Spoonacular. Produces deterministic cost and nutrition
    // derived from the ingredient count, so the full flow works with no API key or
    // network. Selected when Secrets.UseFakeSpoonacular is true.
    public class FakeEnrichmentService : IRecipeEnrichmentService
    {
        private const decimal CostPerIngredientUsd = 1.25m;
        private const decimal CaloriesPerIngredient = 95m;
        private const decimal ProteinPerIngredient = 6m;
        private const decimal FatPerIngredient = 4m;
        private const decimal CarbsPerIngredient = 11m;

        public Task<RecipeEnrichment?> EnrichAsync(IEnumerable<string> ingredients, int servings)
        {
            if (servings <= 0) servings = 1;

            var items = ingredients.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
            if (items.Count == 0)
                return Task.FromResult<RecipeEnrichment?>(null);

            decimal totalUsd = Math.Round(items.Count * CostPerIngredientUsd, 2);
            decimal perServingUsd = Math.Round(totalUsd / servings, 2);

            var nutrition = new NutritionInfo
            {
                Calories     = Math.Round(items.Count * CaloriesPerIngredient / servings, 0),
                ProteinGrams = Math.Round(items.Count * ProteinPerIngredient / servings, 1),
                FatGrams     = Math.Round(items.Count * FatPerIngredient / servings, 1),
                CarbGrams    = Math.Round(items.Count * CarbsPerIngredient / servings, 1),
            };

            return Task.FromResult<RecipeEnrichment?>(
                new RecipeEnrichment(perServingUsd, totalUsd, nutrition));
        }
    }
}
