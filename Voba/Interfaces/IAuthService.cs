using Voba.Models;
using Voba.Services;

namespace Voba.Interfaces
{
    public interface IAuthService
    {
        /// <summary>Registers a new user and returns the created user on success.</summary>
        Task<ServiceResult<User>> RegisterAsync(string email, string username, string password);

        /// <summary>Authenticates the user and returns a JWT on success.</summary>
        Task<ServiceResult<string>> LoginAsync(string email, string password);

        /// <summary>Issues a new JWT from a valid refresh token.</summary>
        Task<ServiceResult<string>> RefreshTokenAsync(string refreshToken);

        /// <summary>Invalidates the refresh token for the given user.</summary>
        Task<bool> LogoutAsync(string userId);
    }
}
