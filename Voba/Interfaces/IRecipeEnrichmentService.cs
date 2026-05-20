using Voba.Models;

namespace Voba.Interfaces
{
    // Per-serving cost + nutrition for a recipe's ingredient list.
    public record RecipeEnrichment(
        decimal CostPerServingUsd,
        decimal TotalCostUsd,
        NutritionInfo Nutrition);

    // Sources cost and nutrition for a set of free-text ingredients.
    // Implementations: SpoonacularEnrichmentService (live API) and
    // FakeEnrichmentService (offline). Returns null when no usable data is found,
    // letting the caller fall back to Gemma's own estimate.
    public interface IRecipeEnrichmentService
    {
        Task<RecipeEnrichment?> EnrichAsync(IEnumerable<string> ingredients, int servings);
    }
}
