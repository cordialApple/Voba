using Voba.Models;
using Voba.Services;

namespace Voba.Interfaces
{
    public record RecipeSuggestion(int Id, string Title, decimal EstimatedCost, string? ImageUrl)
    {
        // Populated when IAiChatService is wired in Step 35 (pending Andre/David branch merge).
        public string? AiDescription { get; init; }
    }

    public interface IRecipeService
    {
        /// <summary>Returns recipe suggestions matching the given ingredients and fitting the budget.</summary>
        Task<ServiceResult<List<RecipeSuggestion>>> GetSuggestionsAsync(
            List<Ingredient> ingredients, decimal budget);
    }
}
