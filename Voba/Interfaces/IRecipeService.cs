using Voba.Models;
using Voba.Services;

namespace Voba.Interfaces
{
    public record RecipeSuggestion(int Id, string Title, decimal EstimatedCost, string? ImageUrl);

    public interface IRecipeService
    {
        /// <summary>Returns recipe suggestions matching the given ingredients and fitting the budget.</summary>
        Task<ServiceResult<List<RecipeSuggestion>>> GetSuggestionsAsync(
            List<Ingredient> ingredients, decimal budget);
    }
}
