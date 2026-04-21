using Voba.Models;

namespace Voba.Interfaces
{
    public interface IGroceryListRepository
    {
        Task<GroceryList?> GetByIdAsync(string id);
        Task<List<GroceryList>> GetByUserIdAsync(string userId);
        Task<GroceryList> SaveAsync(GroceryList list);
        Task<bool> UpdateAsync(GroceryList list);
        Task<bool> DeleteAsync(string id);
    }
}
