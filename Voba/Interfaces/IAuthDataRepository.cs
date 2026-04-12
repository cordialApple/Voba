using Voba.Models;

namespace Voba.Interfaces
{
    public interface IAuthDataRepository
    {
        /// <summary>Returns the auth data for the given user, or null if not found.</summary>
        Task<AuthData?> GetByUserIdAsync(string userId);

        /// <summary>Inserts a new auth data document and returns the saved record.</summary>
        Task<AuthData> SaveAsync(AuthData data);

        /// <summary>Replaces the auth data document matched by Id. Returns true if a document was updated.</summary>
        Task<bool> UpdateAsync(AuthData data);
    }
}
