using Voba.Models;
using Voba.Services;

namespace Voba.Interfaces
{
    public interface IGroceryService
    {
        /// <summary>Builds and saves a budget-optimized grocery list from a Spoonacular recipe.</summary>
        Task<ServiceResult<GroceryList>> CreateFromRecipeAsync(
            int recipeId, string userId, decimal budget);
    }
}
