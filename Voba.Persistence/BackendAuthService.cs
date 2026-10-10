using System.Security.Cryptography;
using System.Text;
using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;
using Voba.Repositories;

namespace Voba.Services;

public sealed class BackendAuthService : IBackendAuthService
{
    private readonly IBackendAccountStore _accounts;
    private readonly IBackendSessionStore _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly BackendTokenService _tokens;
    private readonly TimeProvider _clock;

    public BackendAuthService(IBackendAccountStore accounts, IBackendSessionStore sessions,
        IPasswordHasher hasher, BackendTokenService tokens, TimeProvider clock)
    {
        _accounts = accounts;
        _sessions = sessions;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<ServiceResult<User>> RegisterAsync(string email, string username, string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 ||
            string.IsNullOrWhiteSpace(username) || username.Length > 100 ||
            !ValidPassword(password))
            return ServiceResult<User>.Fail(ErrorCodes.ValidationError, "Invalid registration details.");

        User user;
        try
        {
            user = new User(email, username);
        }
        catch (ArgumentException)
        {
            return ServiceResult<User>.Fail(ErrorCodes.ValidationError, "Invalid registration details.");
        }

        try
        {
            await _accounts.CreateAsync(user, password, _hasher, cancellationToken);
        }
        catch (MongoWriteException error) when (error.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return ServiceResult<User>.Fail(ErrorCodes.ValidationError, "Email already registered.");
        }
        catch (MongoCommandException error) when (error.Code == 11000)
        {
            return ServiceResult<User>.Fail(ErrorCodes.ValidationError, "Email already registered.");
        }

        return ServiceResult<User>.Ok(user);
    }

    public async Task<ServiceResult<AuthTokens>> LoginAsync(string email, string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 ||
            string.IsNullOrEmpty(password) || Encoding.UTF8.GetByteCount(password) > 72)
            return Unauthorized();
        var user = await _accounts.GetUserByEmailAsync(email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null)
            return Unauthorized();
        var auth = await _accounts.GetAuthDataByUserIdAsync(user.Id, cancellationToken);
        if (auth is null || !auth.VerifyPassword(password, _hasher))
            return Unauthorized();

        var refreshToken = CreateRefreshToken();
        var now = _clock.GetUtcNow().UtcDateTime;
        var session = new BackendSession(Guid.NewGuid().ToString("N"), user.Id,
            Hash(refreshToken), now.AddDays(7));
        await _sessions.CreateAsync(session, cancellationToken);
        return ServiceResult<AuthTokens>.Ok(new AuthTokens(
            _tokens.IssueAccessToken(user.Id, session.Id), refreshToken, user.Id));
    }

    public async Task<ServiceResult<AuthTokens>> RefreshAsync(string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(rawRefreshToken) || rawRefreshToken.Length != 43)
            return Unauthorized();
        var replacement = CreateRefreshToken();
        var now = _clock.GetUtcNow().UtcDateTime;
        var session = await _sessions.RotateAsync(Hash(rawRefreshToken), Hash(replacement),
            now, now.AddDays(7), cancellationToken);
        if (session is null)
            return Unauthorized();
        var user = await _accounts.GetUserByIdAsync(session.UserId, cancellationToken);
        if (user is null)
        {
            await _sessions.RevokeAsync(session.Id, session.UserId, cancellationToken);
            return Unauthorized();
        }
        return ServiceResult<AuthTokens>.Ok(new AuthTokens(
            _tokens.IssueAccessToken(user.Id, session.Id), replacement, user.Id));
    }

    public async Task<BackendPrincipal?> AuthenticateAsync(string accessJwt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var claims = _tokens.ValidateAccessToken(accessJwt);
        if (claims is null)
            return null;
        var (userId, sessionId) = claims.Value;
        var active = await _sessions.IsActiveAsync(sessionId, userId,
            _clock.GetUtcNow().UtcDateTime, cancellationToken);
        return active ? new BackendPrincipal(userId, sessionId) : null;
    }

    public Task<bool> LogoutAsync(BackendPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return _sessions.RevokeAsync(principal.SessionId, principal.UserId, cancellationToken);
    }

    private static bool ValidPassword(string? password) =>
        !string.IsNullOrWhiteSpace(password) &&
        Encoding.UTF8.GetByteCount(password) is >= 8 and <= 72;

    private static string CreateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static ServiceResult<AuthTokens> Unauthorized() =>
        ServiceResult<AuthTokens>.Fail(ErrorCodes.Unauthorized, "Invalid credentials or session.");
}
