using System.Text.Json;
using Voba.Interfaces;
using Voba.Models;
using Voba.Services;

namespace Voba.Seed;

public static class SeedRunner
{
    public const string OwnerEmail = "voba-seed-owner@example.invalid";
    public const string OtherEmail = "voba-seed-other@example.invalid";
    public const string DummyPassword = "Voba-Dummy-2026!";
    public const string RecipeTitle = "[VOBA TEST] Bean Soup";

    public static void ValidateDatabaseName(string databaseName)
    {
        if (databaseName == "VobaDemoTests")
            return;
        if (databaseName.StartsWith("voba_seed_", StringComparison.Ordinal) &&
            databaseName.Length > "voba_seed_".Length &&
            databaseName["voba_seed_".Length..].All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '_' or '-'))
            return;
        throw new InvalidOperationException("Seed requires VobaDemoTests or voba_seed_* database.");
    }

    public static async Task RunAsync(IBackendAuthService auth, IUserRecipeStore recipes,
        IRecipeGenerationCache cache, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(recipes);
        ArgumentNullException.ThrowIfNull(cache);
        var users = new List<string>();
        foreach (var (email, name) in new[]
        {
            (OwnerEmail, "Voba Seed Owner"),
            (OtherEmail, "Voba Seed Other")
        })
        {
            await auth.RegisterAsync(email, name, DummyPassword, cancellationToken);
            var login = await auth.LoginAsync(email, DummyPassword, cancellationToken);
            if (!login.Success || login.Data is null)
                throw new InvalidOperationException("Seed account exists with different credentials or login failed.");
            var principal = await auth.AuthenticateAsync(login.Data.AccessToken, cancellationToken);
            if (principal is null || principal.UserId != login.Data.UserId ||
                !await auth.LogoutAsync(principal, cancellationToken))
                throw new InvalidOperationException("Seed account session validation failed.");
            users.Add(login.Data.UserId);
        }
        if (users.Distinct(StringComparer.Ordinal).Count() != 2)
            throw new InvalidOperationException("Seed accounts do not have distinct user IDs.");

        foreach (var userId in users)
        {
            var existing = await recipes.ListAsync(userId, cancellationToken);
            if (existing.All(recipe => recipe.Title != RecipeTitle))
                await recipes.SaveAsync(userId, FullContext(), cancellationToken);
            var fixtureCount = (await recipes.ListAsync(userId, cancellationToken))
                .Count(recipe => recipe.Title == RecipeTitle);
            if (fixtureCount != 1)
                throw new InvalidOperationException("Seed recipe count is not one per account.");
        }

        var now = DateTime.UtcNow;
        var options = OptionsContext();
        var full = FullContext();
        var model = Environment.GetEnvironmentVariable("VOBA_OLLAMA_MODEL") ?? "gemma3:4b";
        foreach (var (key, context) in new[]
        {
            (RecipeGenerationCacheKey.CreateOptions(options, model, RecipeGenerationCacheVersion.Current), options),
            (RecipeGenerationCacheKey.CreateFull(full, model, RecipeGenerationCacheVersion.Current), full)
        })
        {
            await cache.StoreAsync(new RecipeGenerationCacheEntry(key,
                RecipeDataSource.Synthetic, JsonSerializer.Serialize(context), now,
                now.AddDays(30)), cancellationToken);
            var stored = await cache.GetAsync(key, cancellationToken);
            if (stored is null || stored.Source < RecipeDataSource.Synthetic)
                throw new InvalidOperationException("Seed cache entry unavailable.");
        }
    }

    private static RecipeGenerationContext OptionsContext() => new()
    {
        ServingSize = 2,
        TargetBudget = 23.41m,
        CuisinePreference = "Seed Fixture",
        DietaryRestrictions = ["vegan"],
        ProposedOptions = [Option()],
        DataSource = RecipeDataSource.Synthetic
    };

    private static RecipeGenerationContext FullContext() => new()
    {
        ServingSize = 2,
        TargetBudget = 23.41m,
        CuisinePreference = "Seed Fixture",
        DietaryRestrictions = ["vegan"],
        ProposedOptions = [Option()],
        SelectedOption = Option(),
        FinalRecipe = new FullRecipe
        {
            Title = RecipeTitle,
            Instructions = "1. Simmer beans and onion until tender.",
            Nutrition = Nutrition()
        },
        DataSource = RecipeDataSource.Synthetic
    };

    private static RecipeOption Option() => new()
    {
        Name = RecipeTitle,
        Ingredients = ["beans", "onion"],
        EstimatedCost = 3m,
        TotalCost = 6m,
        Nutrition = Nutrition(),
        DataSource = RecipeDataSource.Synthetic,
        NutritionSource = RecipeDataSource.Synthetic
    };

    private static NutritionInfo Nutrition() => new()
    {
        Calories = 210m,
        ProteinGrams = 12m,
        FatGrams = 2m,
        CarbGrams = 36m
    };
}
