using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Voba.Backend;
using Voba.Contracts;
using Voba.Interfaces;
using Voba.Models;
using Voba.Services;
using Xunit;

namespace Voba.Backend.Tests;

public sealed class BackendHttpTests
{
    [Fact]
    public async Task Generation_and_recipes_require_authenticated_session()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IBackendAuthService, StubAuth>();
        await using var app = builder.Build();
        BackendApplication.MapRoutes(app);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        var generation = await client.PostAsJsonAsync("/api/generation/options",
            new GenerationOptionsRequest(20m, 2, [], null));
        var recipes = await client.GetAsync("/api/recipes");

        Assert.Equal(HttpStatusCode.Unauthorized, generation.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, recipes.StatusCode);
    }

    private sealed class StubAuth : IBackendAuthService
    {
        public Task<ServiceResult<User>> RegisterAsync(string email, string username,
            string password, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult<AuthTokens>> LoginAsync(string email, string password,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult<AuthTokens>> RefreshAsync(string rawRefreshToken,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<BackendPrincipal?> AuthenticateAsync(string accessJwt,
            CancellationToken cancellationToken = default) => Task.FromResult<BackendPrincipal?>(null);

        public Task<bool> LogoutAsync(BackendPrincipal principal,
            CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
}
