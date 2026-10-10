using Voba.Models;

namespace Voba.Interfaces;

public interface IUserRecipeStore
{
    Task<Recipe> SaveAsync(string userId, RecipeGenerationContext context,
        CancellationToken cancellationToken = default);
    Task<List<Recipe>> ListAsync(string userId, CancellationToken cancellationToken = default);
    Task<Recipe?> GetAsync(string recipeId, string userId,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string recipeId, string userId,
        CancellationToken cancellationToken = default);
}
