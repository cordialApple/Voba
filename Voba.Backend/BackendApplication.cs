using Microsoft.SemanticKernel;
using MongoDB.Driver;
using Microsoft.AspNetCore.Http.Features;
using Voba.Repositories;
using Voba.Contracts;
using Voba.Interfaces;
using Voba.Models;
using Voba.Services;

namespace Voba.Backend;

public static class BackendApplication
{
    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        ConfigureProductionServices(builder);
        var app = builder.Build();
        MapRoutes(app);
        return app;
    }

    private static void ConfigureProductionServices(WebApplicationBuilder builder)
    {
        var mongoUri = RequiredEnvironment("VOBA_MONGO_CONNECTION_STRING");
        var jwtSecret = RequiredEnvironment("VOBA_JWT_SECRET");
        var databaseName = Environment.GetEnvironmentVariable("VOBA_MONGO_DATABASE") ?? "Voba";
        var model = Environment.GetEnvironmentVariable("VOBA_OLLAMA_MODEL") ?? "gemma3:4b";
        var endpoint = new Uri(Environment.GetEnvironmentVariable("VOBA_OLLAMA_ENDPOINT")
            ?? "http://127.0.0.1:11434");
        var enrichmentMode = (Environment.GetEnvironmentVariable("VOBA_ENRICHMENT_MODE")
            ?? "fake").Trim().ToLowerInvariant();
        if (enrichmentMode is not ("fake" or "real"))
            throw new InvalidOperationException("VOBA_ENRICHMENT_MODE must be 'fake' or 'real'.");
        if (enrichmentMode == "real")
        {
            var key = RequiredEnvironment("VOBA_SPOONACULAR_API_KEY");
            spoonacular.api.ApiSettings.SpoonacularApiKey = key;
        }

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IMongoClient>(_ =>
        {
            var settings = MongoClientSettings.FromConnectionString(mongoUri);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            return new MongoClient(settings);
        });
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>()
            .GetDatabase(databaseName));
        builder.Services.AddSingleton<IBackendAccountStore, MongoBackendAccountStore>();
        builder.Services.AddSingleton<IBackendSessionStore, MongoBackendSessionStore>();
        builder.Services.AddSingleton<IGenerationDraftStore, MongoGenerationDraftStore>();
        builder.Services.AddSingleton<IUserRecipeStore, MongoUserRecipeStore>();
        builder.Services.AddSingleton<IRecipeGenerationCache, MongoRecipeGenerationCache>();
        builder.Services.AddSingleton<IPasswordHasher, BackendPasswordHasher>();
        builder.Services.AddSingleton(_ => new BackendTokenService(jwtSecret,
            "Voba.Backend", "Voba.Desktop"));
        builder.Services.AddSingleton<IBackendAuthService, BackendAuthService>();
        builder.Services.AddSingleton(sp => new RecipeGenerationCoordinator(
            sp.GetRequiredService<IRecipeGenerationCache>(),
            sp.GetRequiredService<TimeProvider>(), TimeSpan.FromHours(24), model,
            RecipeGenerationCacheVersion.Current));
        if (enrichmentMode == "fake")
            builder.Services.AddSingleton<IRecipeEnrichmentService, FakeEnrichmentService>();
        else
        {
            builder.Services.AddSingleton<Voba.Spoonacular.SpoonacularService>();
            builder.Services.AddSingleton<IRecipeEnrichmentService, SpoonacularEnrichmentService>();
        }
        builder.Services.AddKernel().AddOllamaChatCompletion(model, endpoint);
        builder.Services.AddSingleton<IRecipeGenerator, GemmaRecipeGenerator>();
        builder.Services.AddSingleton(sp => new RecipeWorkflowService(
            sp.GetRequiredService<IGenerationDraftStore>(),
            sp.GetRequiredService<RecipeGenerationCoordinator>(),
            sp.GetRequiredService<IRecipeGenerator>(),
            sp.GetRequiredService<TimeProvider>(),
            enrichmentMode == "fake" ? RecipeDataSource.Synthetic : RecipeDataSource.Real,
            TimeSpan.FromHours(1)));
    }

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value &&
        !string.IsNullOrWhiteSpace(value)
            ? value : throw new InvalidOperationException($"{name} is required.");

    public static void MapRoutes(WebApplication app)
    {
        app.Use(async (http, next) =>
        {
            const long maxBodyBytes = 16_384;
            var limit = http.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (limit is { IsReadOnly: false })
                limit.MaxRequestBodySize = maxBodyBytes;
            if (http.Request.ContentLength > maxBodyBytes)
            {
                http.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }
            await next(http);
        });

        app.MapPost("/api/auth/register", async (RegisterRequest request,
            IBackendAuthService auth, CancellationToken cancellationToken) =>
        {
            var result = await auth.RegisterAsync(request.Email, request.Username,
                request.Password, cancellationToken);
            return result.Success && result.Data is { } user
                ? Results.Created($"/api/users/{user.Id}",
                    new RegisterResponse(user.Id, user.Email, user.Username))
                : Results.BadRequest(new ApiError(result.ErrorCode ?? "validation",
                    result.ErrorMessage ?? "Registration failed."));
        });

        app.MapPost("/api/auth/login", async (LoginRequest request,
            IBackendAuthService auth, CancellationToken cancellationToken) =>
        {
            var result = await auth.LoginAsync(request.Email, request.Password, cancellationToken);
            return result.Success && result.Data is { } tokens
                ? Results.Ok(MapTokens(tokens))
                : Results.Unauthorized();
        });

        app.MapPost("/api/auth/refresh", async (RefreshRequest request,
            IBackendAuthService auth, CancellationToken cancellationToken) =>
        {
            var result = await auth.RefreshAsync(request.RefreshToken, cancellationToken);
            return result.Success && result.Data is { } tokens
                ? Results.Ok(MapTokens(tokens))
                : Results.Unauthorized();
        });

        app.MapPost("/api/auth/logout", async (HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var auth = http.RequestServices.GetRequiredService<IBackendAuthService>();
            await auth.LogoutAsync(GetPrincipal(http), cancellationToken);
            return Results.NoContent();
        }).AddEndpointFilter<SessionAuthenticationFilter>();

        var generation = app.MapGroup("/api/generation")
            .AddEndpointFilter<SessionAuthenticationFilter>();
        generation.MapPost("/options", async (GenerationOptionsRequest request,
            HttpContext http, CancellationToken cancellationToken) =>
        {
            var workflow = http.RequestServices.GetRequiredService<RecipeWorkflowService>();
            try
            {
                var response = await workflow.CreateOptionsAsync(GetPrincipal(http).UserId,
                    request, cancellationToken);
                return Results.Ok(response);
            }
            catch (ArgumentException error)
            {
                return Results.BadRequest(new ApiError("validation", error.Message));
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("Recipe generation unavailable.", statusCode: 503);
            }
        });

        generation.MapPost("/drafts/{draftId}/select", async (string draftId,
            SelectRecipeRequest request, HttpContext http, CancellationToken cancellationToken) =>
        {
            var workflow = http.RequestServices.GetRequiredService<RecipeWorkflowService>();
            try
            {
                var response = await workflow.SelectAsync(GetPrincipal(http).UserId,
                    draftId, request, cancellationToken);
                return response is null ? Results.NotFound() : Results.Ok(response);
            }
            catch (ArgumentException error)
            {
                return Results.BadRequest(new ApiError("validation", error.Message));
            }
            catch (DraftConflictException)
            {
                return Results.Conflict(new ApiError("conflict", "Draft changed. Reload options."));
            }
            catch (InvalidOperationException)
            {
                return Results.Problem("Recipe generation unavailable.", statusCode: 503);
            }
        });

        var recipes = app.MapGroup("/api/recipes")
            .AddEndpointFilter<SessionAuthenticationFilter>();
        recipes.MapPost("/", async (SaveRecipeRequest request, HttpContext http,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.DraftId) || request.DraftVersion <= 0)
                return Results.BadRequest(new ApiError("validation", "Draft ID and version required."));
            var userId = GetPrincipal(http).UserId;
            var drafts = http.RequestServices.GetRequiredService<IGenerationDraftStore>();
            var draft = await drafts.GetAsync(request.DraftId, userId, cancellationToken);
            if (draft is null)
                return Results.NotFound();
            if (draft.Version != request.DraftVersion)
                return Results.Conflict(new ApiError("stale", "Recipe selection changed."));
            if (draft.Context.SelectedOption is null ||
                string.IsNullOrWhiteSpace(draft.Context.FinalRecipe?.Instructions))
                return Results.Conflict(new ApiError("incomplete", "Select a complete recipe first."));
            var store = http.RequestServices.GetRequiredService<IUserRecipeStore>();
            var saved = await store.SaveAsync(userId, draft.Context, cancellationToken);
            return Results.Created($"/api/recipes/{saved.Id}", MapRecipe(saved));
        });

        recipes.MapGet("/", async (HttpContext http, CancellationToken cancellationToken) =>
        {
            var store = http.RequestServices.GetRequiredService<IUserRecipeStore>();
            var result = await store.ListAsync(GetPrincipal(http).UserId, cancellationToken);
            return Results.Ok(result.Select(MapRecipe).ToArray());
        });

        recipes.MapGet("/{id}", async (string id, HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var store = http.RequestServices.GetRequiredService<IUserRecipeStore>();
            var recipe = await store.GetAsync(id, GetPrincipal(http).UserId, cancellationToken);
            return recipe is null ? Results.NotFound() : Results.Ok(MapRecipe(recipe));
        });

        recipes.MapDelete("/{id}", async (string id, HttpContext http,
            CancellationToken cancellationToken) =>
        {
            var store = http.RequestServices.GetRequiredService<IUserRecipeStore>();
            return await store.DeleteAsync(id, GetPrincipal(http).UserId, cancellationToken)
                ? Results.NoContent() : Results.NotFound();
        });
    }

    private static BackendPrincipal GetPrincipal(HttpContext http) =>
        (BackendPrincipal)http.Items[typeof(BackendPrincipal)]!;

    private static AuthTokensResponse MapTokens(AuthTokens tokens) =>
        new(tokens.UserId, tokens.AccessToken, tokens.RefreshToken);

    private static SavedRecipeResponse MapRecipe(Recipe recipe) => new(
        recipe.Id, recipe.Title,
        recipe.Ingredients.Select(ingredient => new SavedIngredientResponse(
            ingredient.Name, ingredient.Quantity, ingredient.Unit)).ToArray(),
        recipe.EstimatedCost, recipe.Instructions,
        recipe.Nutrition is { HasData: true } nutrition
            ? new NutritionResponse(nutrition.Calories, nutrition.ProteinGrams,
                nutrition.FatGrams, nutrition.CarbGrams)
            : null,
        recipe.DataSource.ToString(), recipe.NutritionSource.ToString(), recipe.SavedAt,
        recipe.Servings, recipe.Budget, recipe.DietaryRestrictions,
        recipe.CuisinePreference);
}
