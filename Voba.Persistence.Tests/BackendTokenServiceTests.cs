using System.Security.Cryptography;
using Voba.Services;
using Xunit;

namespace Voba.Persistence.Tests;

public sealed class BackendTokenServiceTests
{
    [Fact]
    public void Access_token_requires_matching_issuer_and_audience()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var issuer = new BackendTokenService(key, "voba-test", "voba-client");
        var wrongAudience = new BackendTokenService(key, "voba-test", "other-client");
        var accessToken = issuer.IssueAccessToken("user-id", "session-id");

        Assert.Equal(("user-id", "session-id"), issuer.ValidateAccessToken(accessToken));
        Assert.Null(wrongAudience.ValidateAccessToken(accessToken));
    }

    [Fact]
    public void Signing_key_must_be_stable_and_strong()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new BackendTokenService(Convert.ToBase64String(new byte[16]), "voba-test", "voba-client"));
    }

    [Fact]
    public void Access_token_expires_after_fifteen_minutes()
    {
        var clock = new ManualTimeProvider();
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var service = new BackendTokenService(key, "voba-test", "voba-client", clock);
        var token = service.IssueAccessToken("user-id", "session-id");

        clock.Advance(TimeSpan.FromMinutes(16));

        Assert.Null(service.ValidateAccessToken(token));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
