using Voba.Models;

namespace Voba.Interfaces
{
    public interface IAuthDataRepository
    {
        Task<AuthData?> GetByUserIdAsync(string userId);
        Task<AuthData> SaveAsync(AuthData data);
        Task<bool> UpdateAsync(AuthData data);
        Task<AuthData?> GetByRefreshTokenAsync(string refreshToken);
    }
}
