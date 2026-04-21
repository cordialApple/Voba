using Voba.Models;
using Voba.Services;

namespace Voba.Interfaces
{
    public record RecipeSuggestion(int Id, string Title, decimal EstimatedCost, string? ImageUrl);

    public interface IRecipeService
    {
        Task<ServiceResult<List<RecipeSuggestion>>> GetSuggestionsAsync(
            List<Ingredient> ingredients, decimal budget);
    }
}
