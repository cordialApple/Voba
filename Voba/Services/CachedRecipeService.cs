using Microsoft.Extensions.Caching.Memory;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Services
{
    /// <summary>
    /// Decorator — caches GetSuggestionsAsync results for 10 minutes via IMemoryCache.
    /// </summary>
    public class CachedRecipeService : IRecipeService
    {
        private readonly IRecipeService _inner;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        public CachedRecipeService(IRecipeService inner, IMemoryCache cache)
        {
            _inner = inner;
            _cache = cache;
        }

        /// <summary>Returns cached recipe suggestions; fetches and caches on miss.</summary>
        public async Task<ServiceResult<List<RecipeSuggestion>>> GetSuggestionsAsync(
            List<Ingredient> ingredients, decimal budget)
        {
            var key = $"recipe:{string.Join(",", ingredients.Select(i => i.Name).OrderBy(n => n))}:{budget}";

            if (_cache.TryGetValue(key, out ServiceResult<List<RecipeSuggestion>>? cached) && cached != null)
                return cached;

            var result = await _inner.GetSuggestionsAsync(ingredients, budget);

            if (result.Success)
                _cache.Set(key, result, CacheTtl);

            return result;
        }
    }
}
