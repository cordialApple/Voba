using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;
using Voba.Interfaces;
using Voba.Models;
using Voba.Repositories;
using Voba.Services;
using Xunit;

namespace Voba.Persistence.Tests;

public sealed class BackendAuthServiceTests
{
    [Fact]
    public async Task Registration_rejects_password_over_72_utf8_bytes_before_write()
    {
        var accounts = new MemoryAccounts();
        var service = CreateService(accounts, new MemorySessions());

        var result = await service.RegisterAsync("a@example.com", "A", new string('é', 37));

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.ValidationError, result.ErrorCode);
        Assert.Equal(0, accounts.CreateCalls);
    }

    [Fact]
    public async Task Refresh_token_rotates_once_and_replay_fails()
    {
        var accounts = new MemoryAccounts();
        var sessions = new MemorySessions();
        var service = CreateService(accounts, sessions);
        Assert.True((await service.RegisterAsync("a@example.com", "A", "long enough password")).Success);
        var login = await service.LoginAsync("a@example.com", "long enough password");
        Assert.True(login.Success);

        var first = await service.RefreshAsync(login.Data!.RefreshToken);
        var replay = await service.RefreshAsync(login.Data.RefreshToken);

        Assert.True(first.Success);
        Assert.False(replay.Success);
        Assert.Equal(ErrorCodes.Unauthorized, replay.ErrorCode);
        Assert.NotEqual(login.Data.RefreshToken, first.Data!.RefreshToken);
        Assert.DoesNotContain(login.Data.RefreshToken, sessions.StoredHashes);
    }

    [Fact]
    public async Task Logout_rejects_access_token_after_session_revocation()
    {
        var service = CreateService(new MemoryAccounts(), new MemorySessions());
        Assert.True((await service.RegisterAsync("a@example.com", "A", "long enough password")).Success);
        var login = await service.LoginAsync("a@example.com", "long enough password");
        var principal = await service.AuthenticateAsync(login.Data!.AccessToken);
        Assert.NotNull(principal);

        Assert.True(await service.LogoutAsync(principal!));

        Assert.Null(await service.AuthenticateAsync(login.Data.AccessToken));
        Assert.False((await service.RefreshAsync(login.Data.RefreshToken)).Success);
    }

    [Fact]
    public async Task Empty_refresh_token_never_reaches_store()
    {
        var sessions = new MemorySessions();
        var service = CreateService(new MemoryAccounts(), sessions);

        var result = await service.RefreshAsync(" ");

        Assert.False(result.Success);
        Assert.Equal(0, sessions.RotateCalls);
    }

    [Fact]
    public async Task Oversized_refresh_token_never_reaches_store()
    {
        var sessions = new MemorySessions();
        var service = CreateService(new MemoryAccounts(), sessions);

        var result = await service.RefreshAsync(new string('x', 4096));

        Assert.False(result.Success);
        Assert.Equal(0, sessions.RotateCalls);
    }

    [Fact]
    public async Task Oversized_login_password_never_reaches_store()
    {
        var accounts = new MemoryAccounts();
        var service = CreateService(accounts, new MemorySessions());

        var result = await service.LoginAsync("a@example.com", new string('é', 37));

        Assert.False(result.Success);
        Assert.Equal(0, accounts.EmailLookups);
    }

    private static BackendAuthService CreateService(MemoryAccounts accounts, MemorySessions sessions) =>
        new(accounts, sessions, new TestHasher(),
            new BackendTokenService(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                "voba-test", "voba-client"), TimeProvider.System);

    private sealed class TestHasher : IPasswordHasher
    {
        public string GenerateSalt() => "salt";
        public string Hash(string plainText, string salt) => $"{salt}:{plainText}";
        public bool Verify(string plainText, string hash) => hash == Hash(plainText, "salt");
    }

    private sealed class MemoryAccounts : IBackendAccountStore
    {
        private readonly Dictionary<string, (User User, AuthData Auth)> _accounts = [];
        public int CreateCalls { get; private set; }
        public int EmailLookups { get; private set; }

        public Task CreateAsync(User user, string password, IPasswordHasher hasher,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, ObjectId.GenerateNewId().ToString());
            var auth = new AuthData(user.Id);
            auth.SetPassword(password, hasher);
            _accounts.Add(user.Email, (user, auth));
            return Task.CompletedTask;
        }

        public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            EmailLookups++;
            return Task.FromResult(_accounts.TryGetValue(email.Trim().ToLowerInvariant(), out var account)
                ? account.User : null);
        }

        public Task<User?> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_accounts.Values.Select(value => value.User)
                .FirstOrDefault(user => user.Id == userId));

        public Task<AuthData?> GetAuthDataByUserIdAsync(string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_accounts.Values.Select(value => value.Auth)
                .FirstOrDefault(auth => auth.UserId == userId));
    }

    private sealed class MemorySessions : IBackendSessionStore
    {
        private readonly Dictionary<string, BackendSession> _sessions = [];
        public int RotateCalls { get; private set; }
        public IEnumerable<string> StoredHashes => _sessions.Values.Select(session => session.RefreshHash);

        public Task CreateAsync(BackendSession session, CancellationToken cancellationToken = default)
        {
            _sessions.Add(session.Id, session);
            return Task.CompletedTask;
        }

        public Task<BackendSession?> RotateAsync(string oldRefreshHash, string newRefreshHash,
            DateTime nowUtc, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
        {
            RotateCalls++;
            lock (_sessions)
            {
                var session = _sessions.Values.FirstOrDefault(value =>
                    value.RefreshHash == oldRefreshHash && value.RevokedAtUtc is null &&
                    value.ExpiresAtUtc > nowUtc);
                if (session is null)
                    return Task.FromResult<BackendSession?>(null);
                var rotated = session with { RefreshHash = newRefreshHash, ExpiresAtUtc = expiresAtUtc };
                _sessions[session.Id] = rotated;
                return Task.FromResult<BackendSession?>(rotated);
            }
        }

        public Task<bool> IsActiveAsync(string sessionId, string userId, DateTime nowUtc,
            CancellationToken cancellationToken = default) => Task.FromResult(
            _sessions.TryGetValue(sessionId, out var session) && session.UserId == userId &&
            session.RevokedAtUtc is null && session.ExpiresAtUtc > nowUtc);

        public Task<bool> RevokeAsync(string sessionId, string userId,
            CancellationToken cancellationToken = default)
        {
            if (!_sessions.TryGetValue(sessionId, out var session) || session.UserId != userId)
                return Task.FromResult(false);
            _sessions[sessionId] = session with { RevokedAtUtc = DateTime.UtcNow };
            return Task.FromResult(true);
        }
    }
}
