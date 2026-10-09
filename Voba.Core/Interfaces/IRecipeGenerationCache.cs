using Voba.Models;

namespace Voba.Interfaces;

public interface IRecipeGenerationCache
{
    Task<RecipeGenerationCacheEntry?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<RecipeGenerationCacheEntry> StoreAsync(RecipeGenerationCacheEntry entry, CancellationToken cancellationToken = default);
}
