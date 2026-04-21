namespace Voba.Interfaces
{
    public record SpoonacularSearchResult(int Id, string Title, string? ImageUrl);
    public record SpoonacularIngredient(string Name, decimal Amount, string Unit, decimal EstimatedCost);
    public record SpoonacularRecipeDetail(int Id, string Title, List<SpoonacularIngredient> Ingredients, decimal TotalCost);

    public interface ISpoonacularService
    {
        Task<List<SpoonacularSearchResult>?> SearchRecipesAsync(string query, int number = 5);
        Task<SpoonacularRecipeDetail?> GetRecipeByIdAsync(int recipeId);
    }
}
