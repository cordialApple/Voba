using Voba.Interfaces;
using Voba.Models;

namespace Voba.Repositories;

public sealed record BackendSession(string Id, string UserId, string RefreshHash,
    DateTime ExpiresAtUtc, DateTime? RevokedAtUtc = null);

public interface IBackendAccountStore
{
    Task CreateAsync(User user, string password, IPasswordHasher hasher,
        CancellationToken cancellationToken = default);
    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<AuthData?> GetAuthDataByUserIdAsync(string userId,
        CancellationToken cancellationToken = default);
}

public interface IBackendSessionStore
{
    Task CreateAsync(BackendSession session, CancellationToken cancellationToken = default);
    Task<BackendSession?> RotateAsync(string oldRefreshHash, string newRefreshHash,
        DateTime nowUtc, DateTime expiresAtUtc, CancellationToken cancellationToken = default);
    Task<bool> IsActiveAsync(string sessionId, string userId, DateTime nowUtc,
        CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string sessionId, string userId,
        CancellationToken cancellationToken = default);
}
