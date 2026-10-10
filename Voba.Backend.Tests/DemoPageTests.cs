using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Voba.Backend;
using Voba.Interfaces;
using Voba.Models;
using Voba.Services;
using Xunit;

namespace Voba.Backend.Tests;

public sealed class DemoPageTests
{
    [Fact]
    public async Task Root_opens_same_origin_recipe_demo()
    {
        var contentRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../Voba.Backend"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = contentRoot,
            WebRootPath = Path.Combine(contentRoot, "wwwroot")
        });
        builder.Services.AddSingleton<IBackendAuthService, StubAuth>();
        await using var app = builder.Build();
        BackendApplication.MapRoutes(app);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(address)
        };

        using var root = await http.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, root.StatusCode);
        Assert.Equal("/demo", root.Headers.Location?.OriginalString);

        using var demo = await http.GetAsync("/demo");
        Assert.Equal(HttpStatusCode.OK, demo.StatusCode);
        Assert.Equal("text/html", demo.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Plan dinner", await demo.Content.ReadAsStringAsync());

        foreach (var path in new[] { "/demo.css", "/demo.mjs", "/api.mjs" })
        {
            using var asset = await http.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
            Assert.NotEmpty(await asset.Content.ReadAsStringAsync());
        }

        using var direct = await http.GetAsync("/index.html");
        Assert.Equal(HttpStatusCode.OK, direct.StatusCode);
        Assert.Contains("default-src 'none'", direct.Headers.GetValues(
            "Content-Security-Policy").Single());
        Assert.Contains("no-store", direct.Headers.CacheControl?.ToString());
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
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> LogoutAsync(BackendPrincipal principal,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
