using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Voba.Backend;
using Voba.Contracts;
using Voba.Interfaces;
using Voba.Models;
using Voba.Repositories;
using Voba.Services;
using Xunit;

namespace Voba.Backend.AcceptanceTests;

public sealed class LiveBackendHttpTests
{
    [Fact]
    public async Task Real_http_auth_ownership_cache_restart_and_provider_failure()
    {
        var uri = Environment.GetEnvironmentVariable("VOBA_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri))
            throw new InvalidOperationException("VOBA_TEST_MONGO_URI is required for live acceptance.");
        var settings = MongoClientSettings.FromConnectionString(uri);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(15);
        var client = new MongoClient(settings);
        var database = client.GetDatabase(Environment.GetEnvironmentVariable(
            "VOBA_TEST_MONGO_DATABASE") ?? "VobaDemoTests");
        var runId = Guid.NewGuid().ToString("N");
        var names = new Collections(runId);
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var emailA = $"voba-api-a-{runId}@example.invalid";
        var emailB = $"voba-api-b-{runId}@example.invalid";
        const string password = "VobaDemo!123";
        var request = new GenerationOptionsRequest(20m, 2, ["vegan"], "Italian");

        try
        {
            var generator = new Generator();
            await using (var host = await StartAsync(database, names, key, generator))
            {
                var http = host.Client;
                Assert.Equal(HttpStatusCode.Created,
                    (await http.PostAsJsonAsync("/api/auth/register",
                        new RegisterRequest(emailA, "Test A", password))).StatusCode);
                Assert.Equal(HttpStatusCode.Created,
                    (await http.PostAsJsonAsync("/api/auth/register",
                        new RegisterRequest(emailB, "Test B", password))).StatusCode);

                var loginA = await LoginAsync(http, emailA, password);
                var loginB = await LoginAsync(http, emailB, password);
                Authorize(http, "invalid-token");
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await http.GetAsync("/api/recipes")).StatusCode);
                var sid = new JwtSecurityTokenHandler().ReadJwtToken(loginA.AccessToken)
                    .Claims.Single(claim => claim.Type == "sid").Value;
                var expired = new JwtSecurityToken(
                    issuer: "Voba.Backend.Tests", audience: "Voba.Acceptance",
                    claims: [new Claim(JwtRegisteredClaimNames.Sub, loginA.UserId),
                        new Claim("sid", sid)],
                    notBefore: DateTime.UtcNow.AddHours(-1),
                    expires: DateTime.UtcNow.AddMinutes(-1),
                    signingCredentials: new SigningCredentials(
                        new SymmetricSecurityKey(Convert.FromBase64String(key)),
                        SecurityAlgorithms.HmacSha256));
                Authorize(http, new JwtSecurityTokenHandler().WriteToken(expired));
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await http.GetAsync("/api/recipes")).StatusCode);

                Authorize(http, loginA.AccessToken);
                var optionsResponse = await http.PostAsJsonAsync("/api/generation/options", request);
                Assert.Equal(HttpStatusCode.OK, optionsResponse.StatusCode);
                var options = await optionsResponse.Content.ReadFromJsonAsync<GenerationOptionsResponse>();
                Assert.NotNull(options);
                Assert.Equal("Synthetic", options.Options[0].CostSource);
                Assert.Equal(1, generator.OptionCalls);

                Authorize(http, loginB.AccessToken);
                Assert.Equal(HttpStatusCode.NotFound,
                    (await http.PostAsJsonAsync(
                        $"/api/generation/drafts/{options.DraftId}/select",
                        new SelectRecipeRequest(options.Options[0].OptionId))).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound,
                    (await http.PostAsJsonAsync("/api/recipes",
                        new SaveRecipeRequest(options.DraftId, 1))).StatusCode);

                Authorize(http, loginA.AccessToken);
                var selection = await http.PostAsJsonAsync(
                    $"/api/generation/drafts/{options.DraftId}/select",
                    new SelectRecipeRequest(options.Options[0].OptionId));
                Assert.Equal(HttpStatusCode.OK, selection.StatusCode);
                var full = await selection.Content.ReadFromJsonAsync<FullRecipeResponse>();
                Assert.NotNull(full);
                var saveResponse = await http.PostAsJsonAsync("/api/recipes",
                    new { draftId = options.DraftId, draftVersion = full.DraftVersion,
                        userId = loginB.UserId, costSource = "Real" });
                Assert.Equal(HttpStatusCode.Created, saveResponse.StatusCode);
                var saved = await saveResponse.Content.ReadFromJsonAsync<SavedRecipeResponse>();
                Assert.NotNull(saved);
                Assert.Equal("Synthetic", saved.CostSource);

                Authorize(http, loginB.AccessToken);
                Assert.Equal(HttpStatusCode.NotFound,
                    (await http.GetAsync($"/api/recipes/{saved.Id}")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound,
                    (await http.DeleteAsync($"/api/recipes/{saved.Id}")).StatusCode);
                Authorize(http, loginA.AccessToken);
                Assert.Equal(HttpStatusCode.OK,
                    (await http.GetAsync($"/api/recipes/{saved.Id}")).StatusCode);
                Assert.Equal(HttpStatusCode.NoContent,
                    (await http.PostAsync("/api/auth/logout", null)).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await http.GetAsync("/api/recipes")).StatusCode);
            }

            var afterRestart = new Generator();
            await using (var host = await StartAsync(database, names, key, afterRestart))
            {
                var http = host.Client;
                var login = await LoginAsync(http, emailA, password);
                Authorize(http, login.AccessToken);
                var cached = await http.PostAsJsonAsync("/api/generation/options", request);
                Assert.Equal(HttpStatusCode.OK, cached.StatusCode);
                Assert.Equal(0, afterRestart.OptionCalls);

                afterRestart.FailOptions = true;
                var failedRequest = request with { Budget = 21m };
                Assert.Equal(HttpStatusCode.ServiceUnavailable,
                    (await http.PostAsJsonAsync("/api/generation/options", failedRequest)).StatusCode);
                afterRestart.FailOptions = false;
                Assert.Equal(HttpStatusCode.OK,
                    (await http.PostAsJsonAsync("/api/generation/options", failedRequest)).StatusCode);
                Assert.Equal(2, afterRestart.OptionCalls);
            }
        }
        finally
        {
            foreach (var name in names.All)
                await database.DropCollectionAsync(name);
        }
    }

    private static async Task<AuthTokensResponse> LoginAsync(HttpClient http, string email,
        string password)
    {
        var response = await http.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthTokensResponse>())!;
    }

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<Host> StartAsync(IMongoDatabase database, Collections names,
        string jwtKey, Generator generator)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IBackendAccountStore>(new MongoBackendAccountStore(
            database, names.Users, names.AuthData));
        builder.Services.AddSingleton<IBackendSessionStore>(new MongoBackendSessionStore(
            database, names.Sessions));
        builder.Services.AddSingleton<IGenerationDraftStore>(new MongoGenerationDraftStore(
            database, names.Drafts));
        builder.Services.AddSingleton<IUserRecipeStore>(new MongoUserRecipeStore(
            database, names.Recipes));
        builder.Services.AddSingleton<IRecipeGenerationCache>(new MongoRecipeGenerationCache(
            database, names.Cache));
        builder.Services.AddSingleton<IPasswordHasher, BackendPasswordHasher>();
        builder.Services.AddSingleton(new BackendTokenService(jwtKey,
            "Voba.Backend.Tests", "Voba.Acceptance"));
        builder.Services.AddSingleton<IBackendAuthService, BackendAuthService>();
        builder.Services.AddSingleton(sp => new RecipeGenerationCoordinator(
            sp.GetRequiredService<IRecipeGenerationCache>(), TimeProvider.System,
            TimeSpan.FromHours(1), "test-model", "test-prompt"));
        builder.Services.AddSingleton<IRecipeGenerator>(generator);
        builder.Services.AddSingleton(sp => new RecipeWorkflowService(
            sp.GetRequiredService<IGenerationDraftStore>(),
            sp.GetRequiredService<RecipeGenerationCoordinator>(), generator,
            TimeProvider.System, RecipeDataSource.Synthetic, TimeSpan.FromHours(1)));
        var app = builder.Build();
        BackendApplication.MapRoutes(app);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        return new Host(app, new HttpClient { BaseAddress = new Uri(address) });
    }

    private sealed record Collections(string Id)
    {
        public string Users => $"api_users_{Id}";
        public string AuthData => $"api_authdata_{Id}";
        public string Sessions => $"api_sessions_{Id}";
        public string Drafts => $"api_drafts_{Id}";
        public string Recipes => $"api_recipes_{Id}";
        public string Cache => $"api_cache_{Id}";
        public string[] All => [Users, AuthData, Sessions, Drafts, Recipes, Cache];
    }

    private sealed class Host(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class Generator : IRecipeGenerator
    {
        public int OptionCalls { get; private set; }
        public bool FailOptions { get; set; }

        public Task GenerateOptionsAsync(RecipeGenerationContext context,
            CancellationToken cancellationToken)
        {
            OptionCalls++;
            if (FailOptions)
                throw new InvalidOperationException("Provider failed.");
            context.ProposedOptions = [new RecipeOption
            {
                Name = "Vegan Pasta",
                Ingredients = ["tomato", "pasta"],
                EstimatedCost = 5m,
                TotalCost = 10m,
                DataSource = RecipeDataSource.Synthetic,
                NutritionSource = RecipeDataSource.Synthetic
            }];
            return Task.CompletedTask;
        }

        public Task GenerateFullAsync(RecipeGenerationContext context,
            CancellationToken cancellationToken)
        {
            context.FinalRecipe = new FullRecipe
            {
                Title = context.SelectedOption!.Name,
                Instructions = "1. Cook the ingredients."
            };
            return Task.CompletedTask;
        }
    }
}
