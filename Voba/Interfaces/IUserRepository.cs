using Voba.Models;

namespace Voba.Interfaces
{
    public interface IUserRepository
    {
        /// <summary>Returns the user with the given Id, or null if not found.</summary>
        Task<User?> GetByIdAsync(string id);

        /// <summary>Returns the user with the given email address, or null if not found.</summary>
        Task<User?> GetByEmailAsync(string email);

        /// <summary>Inserts a new user document and returns the saved user.</summary>
        Task<User> SaveAsync(User user);

        /// <summary>Replaces the user document matched by Id. Returns true if a document was updated.</summary>
        Task<bool> UpdateAsync(User user);

        /// <summary>Deletes the user document with the given Id. Returns true if a document was deleted.</summary>
        Task<bool> DeleteAsync(string id);
    }
}
