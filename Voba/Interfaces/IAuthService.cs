using Voba.Models;
using Voba.Services;

namespace Voba.Interfaces
{
    public record AuthTokens(string AccessToken, string RefreshToken);

    public interface IAuthService
    {
        /// <summary>Registers a new user and returns the created user on success.</summary>
        Task<ServiceResult<User>> RegisterAsync(string email, string username, string password);

        /// <summary>Authenticates the user and returns an access token and refresh token on success.</summary>
        Task<ServiceResult<AuthTokens>> LoginAsync(string email, string password);

        /// <summary>Issues a new token pair from a valid refresh token.</summary>
        Task<ServiceResult<AuthTokens>> RefreshTokenAsync(string refreshToken);

        /// <summary>Invalidates the refresh token for the given user.</summary>
        Task<bool> LogoutAsync(string userId);
    }
}
