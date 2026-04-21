using Voba.Models;
using Voba.Services;

namespace Voba.Interfaces
{
    public record AuthTokens(string AccessToken, string RefreshToken);

    public interface IAuthService
    {
        Task<ServiceResult<User>> RegisterAsync(string email, string username, string password);
        Task<ServiceResult<AuthTokens>> LoginAsync(string email, string password);
        Task<ServiceResult<AuthTokens>> RefreshTokenAsync(string refreshToken);
        Task<bool> LogoutAsync(string userId);
    }
}
