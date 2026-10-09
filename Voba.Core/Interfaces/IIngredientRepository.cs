using Voba.Models;

namespace Voba.Interfaces
{
    public interface IIngredientRepository
    {
        Task<Ingredient?> GetByNameAsync(string name);
        Task<Ingredient> SaveAsync(Ingredient ingredient);
        Task<bool> UpdateAsync(Ingredient ingredient);
    }
}
