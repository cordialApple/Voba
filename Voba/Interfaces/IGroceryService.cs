using Voba.Models;
using Voba.Services;

namespace Voba.Interfaces
{
    public interface IGroceryService
    {
        Task<ServiceResult<GroceryList>> CreateFromRecipeAsync(
            int recipeId, string userId, decimal budget);
    }
}
