using Voba.Services;
using Xunit;

namespace Voba.AppData.Tests;

[Collection("Environment variables")]
public class AppConfigurationTests
{
    [Fact]
    public void Missing_jwt_secret_uses_valid_ephemeral_key()
    {
        WithVariable("VOBA_JWT_SECRET", null, () =>
        {
            Assert.True(Convert.FromBase64String(AppConfiguration.JwtSecret).Length >= 32);
            Assert.NotNull(new JwtService());
        });
    }

    [Fact]
    public void Malformed_jwt_secret_fails_clearly()
    {
        WithVariable("VOBA_JWT_SECRET", "bad-base64!", () =>
        {
            var error = Assert.Throws<InvalidOperationException>(() => new JwtService());
            Assert.Contains("VOBA_JWT_SECRET", error.Message);
        });
    }

    [Fact]
    public void Unknown_enrichment_mode_fails_clearly()
    {
        WithVariable("VOBA_ENRICHMENT_MODE", "guess", () =>
        {
            var error = Assert.Throws<InvalidOperationException>(() => AppConfiguration.UseFakeEnrichment);
            Assert.Contains("VOBA_ENRICHMENT_MODE", error.Message);
        });
    }

    [Fact]
    public void Sign_out_clears_session()
    {
        var session = new CurrentUserService();
        session.SetUser("user-id", "user@example.com");

        session.Clear();

        Assert.False(session.IsAuthenticated);
        Assert.Null(session.UserId);
        Assert.Null(session.Email);
    }

    private static void WithVariable(string name, string? value, Action action)
    {
        var original = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, value);
            action();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, original);
        }
    }
}
