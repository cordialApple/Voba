using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using MongoDB.Driver;
using Voba.Backend;
using Voba.Client;
using Voba.Contracts;
using Voba.Interfaces;
using Voba.Models;
using Voba.Repositories;
using Voba.Services;
using Xunit;

namespace Voba.Backend.AcceptanceTests;

public sealed class LiveGemmaHttpTests
{
    [LiveModelFact]
    public async Task Real_gemma_vegan_http_flow_reuses_both_phases_after_restart()
    {
        var mongoUri = Environment.GetEnvironmentVariable("VOBA_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(mongoUri))
            throw new InvalidOperationException("VOBA_TEST_MONGO_URI is required for live model acceptance.");
        var settings = MongoClientSettings.FromConnectionString(mongoUri);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(15);
        var database = new MongoClient(settings).GetDatabase(
            Environment.GetEnvironmentVariable("VOBA_TEST_MONGO_DATABASE") ?? "VobaDemoTests");
        var runId = Guid.NewGuid().ToString("N");
        var collections = new Collections(runId);
        var jwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var email = $"voba-gemma-{runId}@example.invalid";
        const string password = "VobaDemo!123";
        var request = new GenerationOptionsRequest(100m, 2, ["vegan"], "Italian");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        try
        {
            string firstTitle;
            string firstInstructions;
            await using (var first = await StartAsync(database, collections, jwtKey, timeout.Token))
            {
                var api = new VobaApiClient(first.Client, new ApiSession());
                await api.RegisterAsync(email, "Gemma Test", password, timeout.Token);
                await api.LoginAsync(email, password, timeout.Token);
                var options = await api.GetOptionsAsync(request, timeout.Token);
                Assert.NotEmpty(options.Options);
                Assert.All(options.Options, option =>
                {
                    Assert.InRange(option.TotalCost, 0.01m, request.Budget);
                    Assert.Equal("Synthetic", option.CostSource);
                    Assert.True(DietaryCompliancePolicy.AllowsOption(new RecipeOption
                    {
                        Name = option.Name,
                        Ingredients = option.Ingredients.ToList()
                    }, request.DietaryRestrictions));
                });
                Assert.Equal(1, first.Generator.OptionCalls);

                var full = await api.SelectAsync(options.DraftId,
                    options.Options[0].OptionId, timeout.Token);
                Assert.False(string.IsNullOrWhiteSpace(full.Instructions));
                Assert.Contains("vegan", full.DietaryRestrictions, StringComparer.OrdinalIgnoreCase);
                Assert.True(DietaryCompliancePolicy.AllowsFullRecipe(new RecipeOption
                {
                    Name = full.SelectedOption.Name,
                    Ingredients = full.SelectedOption.Ingredients.ToList()
                }, full.Instructions, full.DietaryRestrictions));
                Assert.Equal(1, first.Generator.FullCalls);
                firstTitle = full.Title;
                firstInstructions = full.Instructions;

                var saved = await api.SaveAsync(full.DraftId, full.DraftVersion, timeout.Token);
                Assert.Equal(firstTitle, saved.Title);
                Assert.Equal(firstInstructions, saved.Instructions);
                Assert.Equal("Synthetic", saved.CostSource);
                Assert.Contains(await api.ListAsync(timeout.Token), recipe => recipe.Id == saved.Id);
                Assert.Equal(saved.Id, (await api.GetAsync(saved.Id, timeout.Token)).Id);
            }

            await using (var second = await StartAsync(database, collections, jwtKey, timeout.Token))
            {
                var session = new ApiSession();
                var api = new VobaApiClient(second.Client, session);
                await api.LoginAsync(email, password, timeout.Token);
                var options = await api.GetOptionsAsync(request, timeout.Token);
                Assert.NotEmpty(options.Options);
                var full = await api.SelectAsync(options.DraftId,
                    options.Options[0].OptionId, timeout.Token);
                Assert.Equal(firstTitle, full.Title);
                Assert.Equal(firstInstructions, full.Instructions);
                Assert.Equal(0, second.Generator.OptionCalls);
                Assert.Equal(0, second.Generator.FullCalls);

                var accessToken = session.Snapshot()!.AccessToken;
                await api.LogoutAsync(timeout.Token);
                Assert.False(session.IsAuthenticated);
                using var probe = new HttpRequestMessage(HttpMethod.Get, "/api/recipes");
                probe.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                using var denied = await second.Client.SendAsync(probe, timeout.Token);
                Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
            }
        }
        finally
        {
            foreach (var name in collections.All)
                await database.DropCollectionAsync(name);
        }
    }

    private static async Task<Host> StartAsync(IMongoDatabase database, Collections names,
        string jwtKey, CancellationToken cancellationToken)
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
            "Voba.Backend.ModelTests", "Voba.ModelAcceptance"));
        builder.Services.AddSingleton<IBackendAuthService, BackendAuthService>();
        var model = Environment.GetEnvironmentVariable("VOBA_OLLAMA_MODEL") ?? "gemma3:4b";
        var endpoint = new Uri(Environment.GetEnvironmentVariable("VOBA_OLLAMA_ENDPOINT")
            ?? "http://127.0.0.1:11434");
        builder.Services.AddKernel().AddOllamaChatCompletion(model, endpoint);
        builder.Services.AddSingleton<IRecipeEnrichmentService, FakeEnrichmentService>();
        builder.Services.AddSingleton<CountingGenerator>();
        builder.Services.AddSingleton<IRecipeGenerator>(sp => sp.GetRequiredService<CountingGenerator>());
        builder.Services.AddSingleton(sp => new RecipeGenerationCoordinator(
            sp.GetRequiredService<IRecipeGenerationCache>(), TimeProvider.System,
            TimeSpan.FromHours(24), model, RecipeGenerationCacheVersion.Current));
        builder.Services.AddSingleton(sp => new RecipeWorkflowService(
            sp.GetRequiredService<IGenerationDraftStore>(),
            sp.GetRequiredService<RecipeGenerationCoordinator>(),
            sp.GetRequiredService<IRecipeGenerator>(), TimeProvider.System,
            RecipeDataSource.Synthetic, TimeSpan.FromHours(1)));
        var app = builder.Build();
        BackendApplication.MapRoutes(app);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync(cancellationToken);
        var address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        var client = new HttpClient
        {
            BaseAddress = new Uri(address),
            Timeout = TimeSpan.FromMinutes(5)
        };
        return new Host(app, client, app.Services.GetRequiredService<CountingGenerator>());
    }

    private sealed record Collections(string Id)
    {
        public string Users => $"gemma_users_{Id}";
        public string AuthData => $"gemma_authdata_{Id}";
        public string Sessions => $"gemma_sessions_{Id}";
        public string Drafts => $"gemma_drafts_{Id}";
        public string Recipes => $"gemma_recipes_{Id}";
        public string Cache => $"gemma_cache_{Id}";
        public string[] All => [Users, AuthData, Sessions, Drafts, Recipes, Cache];
    }

    private sealed class Host(WebApplication app, HttpClient client, CountingGenerator generator)
        : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public CountingGenerator Generator { get; } = generator;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class CountingGenerator(Kernel kernel, IRecipeEnrichmentService enrichment)
        : IRecipeGenerator
    {
        private readonly GemmaRecipeGenerator _inner = new(kernel, enrichment);
        public int OptionCalls { get; private set; }
        public int FullCalls { get; private set; }

        public async Task GenerateOptionsAsync(RecipeGenerationContext context,
            CancellationToken cancellationToken)
        {
            OptionCalls++;
            await _inner.GenerateOptionsAsync(context, cancellationToken);
        }

        public async Task GenerateFullAsync(RecipeGenerationContext context,
            CancellationToken cancellationToken)
        {
            FullCalls++;
            await _inner.GenerateFullAsync(context, cancellationToken);
        }
    }
}

public sealed class LiveModelFactAttribute : FactAttribute
{
    public LiveModelFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("VOBA_TEST_LIVE_MODEL") != "1")
            Skip = "Set VOBA_TEST_LIVE_MODEL=1 to run local Gemma acceptance.";
    }
}
