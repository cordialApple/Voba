using Voba.Interfaces;

namespace Voba.api
{
    /// <summary>
    /// Compile stub — satisfies ISpoonacularService in the DI chain.
    /// Replace with Vathana's SDK implementation when his branch merges (see recon-33.5.md § Step 34).
    /// </summary>
    public class SpoonacularService : ISpoonacularService
    {
        /// <summary>Searches Spoonacular for recipes matching the query.</summary>
        public Task<List<SpoonacularSearchResult>?> SearchRecipesAsync(string query, int number = 5)
            => Task.FromResult<List<SpoonacularSearchResult>?>(new List<SpoonacularSearchResult>());

        /// <summary>Retrieves full recipe detail including ingredients and cost by Spoonacular recipe ID.</summary>
        public Task<SpoonacularRecipeDetail?> GetRecipeByIdAsync(int recipeId)
            => Task.FromResult<SpoonacularRecipeDetail?>(null);
    }
}
