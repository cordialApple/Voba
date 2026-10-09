using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using Voba.Backend;
using Voba.Contracts;
using Voba.Interfaces;
using Voba.Models;
using Voba.Services;
using Xunit;

namespace Voba.Backend.Tests;

public sealed class BackendHttpOwnershipTests
{
    private const string UserA = "aaaaaaaaaaaaaaaaaaaaaaaa";
    private const string UserB = "bbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task Two_users_cannot_select_save_read_or_delete_each_others_recipes()
    {
        var drafts = new Drafts();
        var recipes = new Recipes();
        var coordinator = new RecipeGenerationCoordinator(new Cache(), TimeProvider.System,
            TimeSpan.FromHours(1), "test", "test");
        var workflow = new RecipeWorkflowService(drafts, coordinator, new Generator(),
            TimeProvider.System, RecipeDataSource.Synthetic, TimeSpan.FromHours(1));
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IBackendAuthService, Auth>();
        builder.Services.AddSingleton<IGenerationDraftStore>(drafts);
        builder.Services.AddSingleton<IUserRecipeStore>(recipes);
        builder.Services.AddSingleton(workflow);
        await using var app = builder.Build();
        BackendApplication.MapRoutes(app);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        Authorize(client, "a");
        var invalidRequests = new[]
        {
            new GenerationOptionsRequest(0m, 2, [], null),
            new GenerationOptionsRequest(1_000_001m, 2, [], null),
            new GenerationOptionsRequest(20m, 0, [], null),
            new GenerationOptionsRequest(20m, 1001, [], null),
            new GenerationOptionsRequest(20m, 2, Enumerable.Repeat("vegan", 33).ToArray(), null),
            new GenerationOptionsRequest(20m, 2, [new string('x', 201)], null),
            new GenerationOptionsRequest(20m, 2, [], new string('x', 201))
        };
        foreach (var invalid in invalidRequests)
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/generation/options", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,
            (await client.PostAsJsonAsync("/api/generation/options",
                new GenerationOptionsRequest(20m, 2, [], new string('x', 20_000)))).StatusCode);
        var optionsResponse = await client.PostAsJsonAsync("/api/generation/options",
            new { budget = 20m, servings = 2, dietaryRestrictions = new[] { "gluten-free" },
                cuisinePreference = "Italian", userId = UserB, costSource = "Real" });
        Assert.Equal(HttpStatusCode.OK, optionsResponse.StatusCode);
        var options = await optionsResponse.Content.ReadFromJsonAsync<GenerationOptionsResponse>();
        Assert.NotNull(options);
        Assert.Equal("Synthetic", options.Options[0].CostSource);
        Assert.Equal(UserA, (await drafts.GetAsync(options.DraftId, UserA))?.UserId);

        Authorize(client, "b");
        var foreignSelection = await client.PostAsJsonAsync(
            $"/api/generation/drafts/{options.DraftId}/select",
            new SelectRecipeRequest(options.Options[0].OptionId));
        Assert.Equal(HttpStatusCode.NotFound, foreignSelection.StatusCode);

        Authorize(client, "a");
        var selection = await client.PostAsJsonAsync(
            $"/api/generation/drafts/{options.DraftId}/select",
            new SelectRecipeRequest(options.Options[0].OptionId));
        Assert.Equal(HttpStatusCode.OK, selection.StatusCode);
        var firstFull = await selection.Content.ReadFromJsonAsync<FullRecipeResponse>();
        Assert.NotNull(firstFull);
        var nextSelection = await client.PostAsJsonAsync(
            $"/api/generation/drafts/{options.DraftId}/select",
            new SelectRecipeRequest(options.Options[1].OptionId));
        Assert.Equal(HttpStatusCode.OK, nextSelection.StatusCode);
        var latestFull = await nextSelection.Content.ReadFromJsonAsync<FullRecipeResponse>();
        Assert.NotNull(latestFull);
        Assert.NotEqual(firstFull.DraftVersion, latestFull.DraftVersion);

        Authorize(client, "b");
        var foreignSave = await client.PostAsJsonAsync("/api/recipes",
            new { draftId = options.DraftId, draftVersion = latestFull.DraftVersion,
                userId = UserB, costSource = "Real" });
        Assert.Equal(HttpStatusCode.NotFound, foreignSave.StatusCode);

        Authorize(client, "a");
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/recipes", new SaveRecipeRequest("", 1))).StatusCode);
        var staleSave = await client.PostAsJsonAsync("/api/recipes",
            new SaveRecipeRequest(options.DraftId, firstFull.DraftVersion));
        Assert.Equal(HttpStatusCode.Conflict, staleSave.StatusCode);
        var save = await client.PostAsJsonAsync("/api/recipes",
            new { draftId = options.DraftId, draftVersion = latestFull.DraftVersion,
                userId = UserB, costSource = "Real" });
        Assert.Equal(HttpStatusCode.Created, save.StatusCode);
        var saved = await save.Content.ReadFromJsonAsync<SavedRecipeResponse>();
        Assert.NotNull(saved);
        Assert.Equal("Synthetic", saved.CostSource);
        Assert.Equal(2, saved.Servings);
        Assert.Equal(20m, saved.Budget);
        Assert.Equal(["gluten-free"], saved.DietaryRestrictions);
        Assert.Equal("Italian", saved.CuisinePreference);
        Assert.Equal(UserA, recipes.LastSaved?.UserId);

        Authorize(client, "b");
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/recipes/{saved.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.DeleteAsync($"/api/recipes/{saved.Id}")).StatusCode);
        var foreignList = await client.GetFromJsonAsync<SavedRecipeResponse[]>("/api/recipes");
        Assert.Empty(foreignList!);

        Authorize(client, "a");
        var reopened = await client.GetFromJsonAsync<SavedRecipeResponse>(
            $"/api/recipes/{saved.Id}");
        Assert.NotNull(reopened);
        Assert.Equal(2, reopened.Servings);
        Assert.Equal(20m, reopened.Budget);
        Assert.Equal(["gluten-free"], reopened.DietaryRestrictions);
        Assert.Equal("Italian", reopened.CuisinePreference);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/recipes/{saved.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("expired")]
    public async Task Invalid_or_expired_bearer_is_denied(string token)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IBackendAuthService, Auth>();
        await using var app = builder.Build();
        BackendApplication.MapRoutes(app);
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        Authorize(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/recipes")).StatusCode);
    }

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private sealed class Auth : IBackendAuthService
    {
        public Task<BackendPrincipal?> AuthenticateAsync(string accessJwt,
            CancellationToken cancellationToken = default) => Task.FromResult<BackendPrincipal?>(
                accessJwt switch
                {
                    "a" => new BackendPrincipal(UserA, "session-a"),
                    "b" => new BackendPrincipal(UserB, "session-b"),
                    _ => null
                });

        public Task<ServiceResult<User>> RegisterAsync(string email, string username,
            string password, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult<AuthTokens>> LoginAsync(string email, string password,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ServiceResult<AuthTokens>> RefreshAsync(string rawRefreshToken,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> LogoutAsync(BackendPrincipal principal,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class Generator : IRecipeGenerator
    {
        public Task GenerateOptionsAsync(RecipeGenerationContext context,
            CancellationToken cancellationToken)
        {
            context.ProposedOptions = [Option("Pasta"), Option("Risotto")];
            return Task.CompletedTask;
        }

        private static RecipeOption Option(string name) => new()
        {
            Name = name,
            Ingredients = ["tomato", "pasta"],
            EstimatedCost = 5m,
            TotalCost = 10m,
            DataSource = RecipeDataSource.Synthetic,
            NutritionSource = RecipeDataSource.Synthetic
        };

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

    private sealed class Drafts : IGenerationDraftStore
    {
        private readonly Dictionary<string, GenerationDraft> _drafts = new();

        public Task<GenerationDraft> CreateAsync(string userId, RecipeGenerationContext context,
            DateTime expiresAtUtc, CancellationToken cancellationToken = default)
        {
            var draft = new GenerationDraft(Guid.NewGuid().ToString("N"), userId, context,
                expiresAtUtc, 0);
            _drafts[draft.Id] = draft;
            return Task.FromResult(draft);
        }

        public Task<GenerationDraft?> GetAsync(string id, string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_drafts.TryGetValue(id, out var draft) && draft.UserId == userId
                ? draft : null);

        public Task<bool> ReplaceAsync(GenerationDraft original, RecipeGenerationContext context,
            CancellationToken cancellationToken = default)
        {
            if (!_drafts.TryGetValue(original.Id, out var current) ||
                current.UserId != original.UserId || current.Version != original.Version)
                return Task.FromResult(false);
            _drafts[original.Id] = current with { Context = context, Version = current.Version + 1 };
            return Task.FromResult(true);
        }
    }

    private sealed class Cache : IRecipeGenerationCache
    {
        private readonly Dictionary<string, RecipeGenerationCacheEntry> _entries = new();
        public Task<RecipeGenerationCacheEntry?> GetAsync(string key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_entries.GetValueOrDefault(key));
        public Task<RecipeGenerationCacheEntry> StoreAsync(RecipeGenerationCacheEntry entry,
            CancellationToken cancellationToken = default)
        {
            _entries[entry.Key] = entry;
            return Task.FromResult(entry);
        }
    }

    private sealed class Recipes : IUserRecipeStore
    {
        private readonly Dictionary<string, Recipe> _recipes = new();
        public Recipe? LastSaved { get; private set; }

        public Task<Recipe> SaveAsync(string userId, RecipeGenerationContext context,
            CancellationToken cancellationToken = default)
        {
            var recipe = RecipeMapper.ToRecipe(context, userId);
            typeof(Recipe).GetProperty(nameof(Recipe.Id))!
                .SetValue(recipe, ObjectId.GenerateNewId().ToString());
            _recipes[recipe.Id] = recipe;
            LastSaved = recipe;
            return Task.FromResult(recipe);
        }

        public Task<List<Recipe>> ListAsync(string userId,
            CancellationToken cancellationToken = default) => Task.FromResult(
            _recipes.Values.Where(recipe => recipe.UserId == userId).ToList());

        public Task<Recipe?> GetAsync(string recipeId, string userId,
            CancellationToken cancellationToken = default) => Task.FromResult(
            _recipes.TryGetValue(recipeId, out var recipe) && recipe.UserId == userId
                ? recipe : null);

        public Task<bool> DeleteAsync(string recipeId, string userId,
            CancellationToken cancellationToken = default) => Task.FromResult(
            _recipes.TryGetValue(recipeId, out var recipe) && recipe.UserId == userId &&
            _recipes.Remove(recipeId));
    }
}
