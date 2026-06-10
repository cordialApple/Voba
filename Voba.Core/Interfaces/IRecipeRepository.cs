using Voba.Models;

namespace Voba.Interfaces
{
    public interface IRecipeRepository
    {
        Task<List<Recipe>> GetByUserIdAsync(string userId);
        Task<Recipe> SaveAsync(Recipe recipe);
        Task<bool> DeleteAsync(string id);
    }
}
