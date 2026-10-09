using Voba.Models;
using Voba.Services;

namespace Voba.Interfaces;

public interface IBackendAuthService
{
    Task<ServiceResult<User>> RegisterAsync(string email, string username, string password,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthTokens>> LoginAsync(string email, string password,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<AuthTokens>> RefreshAsync(string rawRefreshToken,
        CancellationToken cancellationToken = default);
    Task<BackendPrincipal?> AuthenticateAsync(string accessJwt,
        CancellationToken cancellationToken = default);
    Task<bool> LogoutAsync(BackendPrincipal principal,
        CancellationToken cancellationToken = default);
}
