using Voba.Interfaces;
using Voba.Models;

namespace Voba.Services
{
    public class RecipeService : IRecipeService
    {
        private readonly ISpoonacularService _spoonacular;

        public RecipeService(ISpoonacularService spoonacular)
        {
            _spoonacular = spoonacular;
        }

        public async Task<ServiceResult<List<RecipeSuggestion>>> GetSuggestionsAsync(
            List<Ingredient> ingredients, decimal budget)
        {
            if (ingredients is null || ingredients.Count == 0)
                return ServiceResult<List<RecipeSuggestion>>.Fail(
                    ErrorCodes.ValidationError, "At least one ingredient is required.");

            if (budget <= 0)
                return ServiceResult<List<RecipeSuggestion>>.Fail(
                    ErrorCodes.ValidationError, "Budget must be positive.");

            var query = string.Join(",", ingredients.Select(i => i.Name));
            var searchResults = await _spoonacular.SearchRecipesAsync(query);
            if (searchResults is null || searchResults.Count == 0)
                return ServiceResult<List<RecipeSuggestion>>.Fail(
                    ErrorCodes.NotFound, "No recipes found for the given ingredients.");

            var suggestions = new List<RecipeSuggestion>();
            foreach (var result in searchResults)
            {
                var detail = await _spoonacular.GetRecipeByIdAsync(result.Id);
                if (detail is null || detail.TotalCost > budget) continue;

                suggestions.Add(new RecipeSuggestion(
                    result.Id, result.Title, detail.TotalCost, result.ImageUrl));
            }

            return ServiceResult<List<RecipeSuggestion>>.Ok(suggestions);
        }
    }
}
