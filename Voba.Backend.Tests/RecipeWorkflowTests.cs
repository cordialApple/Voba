using Voba.Backend;
using Voba.Contracts;
using Voba.Interfaces;
using Voba.Models;
using Voba.Services;
using System.Text.Json;
using Xunit;

namespace Voba.Backend.Tests;

public sealed class RecipeWorkflowTests
{
    [Fact]
    public async Task Options_create_user_bound_draft_and_cache_reuses_generation()
    {
        var drafts = new MemoryDrafts();
        var generator = new CountingGenerator();
        var cache = new MemoryCache();
        var coordinator = new RecipeGenerationCoordinator(cache, TimeProvider.System,
            TimeSpan.FromHours(1), "test-model", "test-prompt");
        var workflow = new RecipeWorkflowService(drafts, coordinator, generator,
            TimeProvider.System, RecipeDataSource.Synthetic, TimeSpan.FromMinutes(30));
        var request = new GenerationOptionsRequest(20m, 2, ["vegan"], "Italian");

        var first = await workflow.CreateOptionsAsync("user-a", request);
        var second = await workflow.CreateOptionsAsync("user-a", request);

        Assert.NotEqual(first.DraftId, second.DraftId);
        Assert.Single(first.Options);
        Assert.Equal("Synthetic", first.Options[0].CostSource);
        Assert.Equal(1, generator.OptionCalls);
        Assert.NotNull(await drafts.GetAsync(first.DraftId, "user-a"));
        Assert.Null(await drafts.GetAsync(first.DraftId, "user-b"));
    }

    [Fact]
    public async Task Selection_uses_stored_option_and_denies_foreign_draft()
    {
        var drafts = new MemoryDrafts();
        var generator = new CountingGenerator();
        var workflow = NewWorkflow(drafts, generator, new MemoryCache());
        var options = await workflow.CreateOptionsAsync("user-a",
            new GenerationOptionsRequest(20m, 2, [], null));

        var foreign = await workflow.SelectAsync("user-b", options.DraftId,
            new SelectRecipeRequest(options.Options[0].OptionId));
        var full = await workflow.SelectAsync("user-a", options.DraftId,
            new SelectRecipeRequest(options.Options[0].OptionId));

        Assert.Null(foreign);
        Assert.NotNull(full);
        Assert.Equal("Pasta", full.Title);
        Assert.Equal("Synthetic", full.SelectedOption.CostSource);
        Assert.Equal(1, generator.FullCalls);
        Assert.NotNull((await drafts.GetAsync(options.DraftId, "user-a"))?.Context.FinalRecipe);
    }

    [Fact]
    public async Task Failed_full_generation_leaves_options_draft_unchanged()
    {
        var drafts = new MemoryDrafts();
        var generator = new CountingGenerator();
        var workflow = NewWorkflow(drafts, generator, new MemoryCache());
        var options = await workflow.CreateOptionsAsync("user-a",
            new GenerationOptionsRequest(20m, 2, [], null));
        generator.FailFull = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.SelectAsync("user-a",
            options.DraftId, new SelectRecipeRequest(options.Options[0].OptionId)));

        var draft = await drafts.GetAsync(options.DraftId, "user-a");
        Assert.NotNull(draft);
        Assert.Null(draft.Context.SelectedOption);
        Assert.Null(draft.Context.FinalRecipe);
    }

    [Fact]
    public async Task Failed_options_generation_creates_no_draft()
    {
        var drafts = new MemoryDrafts();
        var generator = new CountingGenerator { FailOptions = true };
        var workflow = NewWorkflow(drafts, generator, new MemoryCache());

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.CreateOptionsAsync("user-a",
            new GenerationOptionsRequest(20m, 2, [], null)));

        Assert.Equal(0, drafts.Count);
    }

    [Fact]
    public async Task Vegan_unsafe_option_creates_no_draft_or_cache_entry()
    {
        var drafts = new MemoryDrafts();
        var generator = new CountingGenerator { OptionIngredients = ["chicken breast", "rice"] };
        var cache = new MemoryCache();
        var workflow = NewWorkflow(drafts, generator, cache);
        var request = new GenerationOptionsRequest(20m, 2, ["vegan"], null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CreateOptionsAsync("user-a", request));
        Assert.Equal(0, drafts.Count);
        Assert.Equal(0, cache.Count);

        generator.OptionIngredients = ["tofu", "rice"];
        var result = await workflow.CreateOptionsAsync("user-a", request);
        Assert.Single(result.Options);
        Assert.Equal(2, generator.OptionCalls);
    }

    [Fact]
    public async Task Vegan_unsafe_instruction_cannot_enter_draft_or_cache()
    {
        var drafts = new MemoryDrafts();
        var generator = new CountingGenerator { Instructions = "1. Add butter to the pasta." };
        var cache = new MemoryCache();
        var workflow = NewWorkflow(drafts, generator, cache);
        var options = await workflow.CreateOptionsAsync("user-a",
            new GenerationOptionsRequest(20m, 2, ["vegan"], null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.SelectAsync("user-a", options.DraftId,
                new SelectRecipeRequest(options.Options[0].OptionId)));
        Assert.Null((await drafts.GetAsync(options.DraftId, "user-a"))!.Context.FinalRecipe);
        Assert.Equal(1, cache.Count);

        generator.Instructions = "1. Simmer pasta in tomato sauce.";
        var full = await workflow.SelectAsync("user-a", options.DraftId,
            new SelectRecipeRequest(options.Options[0].OptionId));
        Assert.NotNull(full);
        Assert.Equal(2, generator.FullCalls);
    }

    [Fact]
    public async Task Cached_unsafe_vegan_option_is_not_served_or_drafted()
    {
        var drafts = new MemoryDrafts();
        var generator = new CountingGenerator();
        var cache = new MemoryCache();
        var context = new RecipeGenerationContext
        {
            TargetBudget = 20m,
            ServingSize = 2,
            DietaryRestrictions = ["vegan"],
            ProposedOptions = [new RecipeOption
            {
                Name = "Chicken dinner",
                Ingredients = ["chicken breast"],
                DataSource = RecipeDataSource.Synthetic,
                EstimatedCost = 4m,
                TotalCost = 8m
            }]
        };
        var now = DateTime.UtcNow;
        await cache.StoreAsync(new RecipeGenerationCacheEntry(
            RecipeGenerationCacheKey.CreateOptions(context, "test-model", "test-prompt"),
            RecipeDataSource.Synthetic, JsonSerializer.Serialize(context), now, now.AddHours(1)));
        var workflow = NewWorkflow(drafts, generator, cache);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            workflow.CreateOptionsAsync("user-a",
                new GenerationOptionsRequest(20m, 2, ["vegan"], null)));

        Assert.Equal(0, generator.OptionCalls);
        Assert.Equal(0, drafts.Count);
    }

    private static RecipeWorkflowService NewWorkflow(MemoryDrafts drafts,
        CountingGenerator generator, MemoryCache cache) => new(
            drafts, new RecipeGenerationCoordinator(cache, TimeProvider.System,
                TimeSpan.FromHours(1), "test-model", "test-prompt"), generator,
            TimeProvider.System, RecipeDataSource.Synthetic, TimeSpan.FromMinutes(30));

    private sealed class CountingGenerator : IRecipeGenerator
    {
        public int OptionCalls { get; private set; }
        public int FullCalls { get; private set; }
        public bool FailOptions { get; set; }
        public bool FailFull { get; set; }
        public List<string> OptionIngredients { get; set; } = ["tomato", "pasta"];
        public string Instructions { get; set; } = "1. Cook the ingredients.";

        public Task GenerateOptionsAsync(RecipeGenerationContext context, CancellationToken cancellationToken)
        {
            if (FailOptions)
                throw new InvalidOperationException("Provider failed.");
            OptionCalls++;
            context.ProposedOptions = [new RecipeOption
            {
                Name = "Pasta",
                Ingredients = OptionIngredients,
                EstimatedCost = 5m,
                TotalCost = 10m,
                DataSource = RecipeDataSource.Synthetic,
                NutritionSource = RecipeDataSource.Synthetic
            }];
            return Task.CompletedTask;
        }

        public Task GenerateFullAsync(RecipeGenerationContext context, CancellationToken cancellationToken)
        {
            if (FailFull)
                throw new InvalidOperationException("Provider failed.");
            FullCalls++;
            context.FinalRecipe = new FullRecipe
            {
                Title = context.SelectedOption!.Name,
                Instructions = Instructions
            };
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryDrafts : IGenerationDraftStore
    {
        private readonly Dictionary<string, GenerationDraft> _drafts = new();
        public int Count => _drafts.Count;

        public Task<GenerationDraft> CreateAsync(string userId, RecipeGenerationContext context,
            DateTime expiresAtUtc, CancellationToken cancellationToken = default)
        {
            var draft = new GenerationDraft(Guid.NewGuid().ToString("N"), userId, context, expiresAtUtc, 0);
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

    private sealed class MemoryCache : IRecipeGenerationCache
    {
        private readonly Dictionary<string, RecipeGenerationCacheEntry> _entries = new();
        public int Count => _entries.Count;

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
}
