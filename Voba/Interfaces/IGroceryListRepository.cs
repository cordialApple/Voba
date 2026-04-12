using Voba.Models;

namespace Voba.Interfaces
{
    public interface IGroceryListRepository
    {
        /// <summary>Returns the grocery list with the given Id, or null if not found.</summary>
        Task<GroceryList?> GetByIdAsync(string id);

        /// <summary>Returns all grocery lists belonging to the given user.</summary>
        Task<List<GroceryList>> GetByUserIdAsync(string userId);

        /// <summary>Inserts a new grocery list document and returns the saved list.</summary>
        Task<GroceryList> SaveAsync(GroceryList list);

        /// <summary>Replaces the grocery list document matched by Id. Returns true if a document was updated.</summary>
        Task<bool> UpdateAsync(GroceryList list);

        /// <summary>Deletes the grocery list document with the given Id. Returns true if a document was deleted.</summary>
        Task<bool> DeleteAsync(string id);
    }
}
