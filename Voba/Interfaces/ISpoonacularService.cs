namespace Voba.Interfaces
{
    public record SpoonacularSearchResult(int Id, string Title, string? ImageUrl);
    public record SpoonacularIngredient(string Name, decimal Amount, string Unit, decimal EstimatedCost);
    public record SpoonacularRecipeDetail(int Id, string Title, List<SpoonacularIngredient> Ingredients, decimal TotalCost);

    public interface ISpoonacularService
    {
        /// <summary>Searches Spoonacular for recipes matching the query.</summary>
        Task<List<SpoonacularSearchResult>?> SearchRecipesAsync(string query, int number = 5);

        /// <summary>Retrieves full recipe detail including ingredients and cost by Spoonacular recipe ID.</summary>
        Task<SpoonacularRecipeDetail?> GetRecipeByIdAsync(int recipeId);
    }
}
