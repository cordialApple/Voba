using Voba.Models;
using Voba.Interfaces;
using Voba.Services;
using Xunit;

namespace Voba.Core.Tests;

public class GenerationCacheTests
{
    [Fact]
    public void Equivalent_requests_share_options_key()
    {
        var first = new RecipeGenerationContext
        {
            ServingSize = 2,
            TargetBudget = 20m,
            CuisinePreference = " Italian ",
            DietaryRestrictions = [" Vegan ", "NO Nuts"]
        };
        var second = new RecipeGenerationContext
        {
            ServingSize = 2,
            TargetBudget = 20.00m,
            CuisinePreference = "italian",
            DietaryRestrictions = ["no   nuts", "vegan", "VEGAN"]
        };

        Assert.Equal(
            RecipeGenerationCacheKey.CreateOptions(first, "gemma:4b", "v1"),
            RecipeGenerationCacheKey.CreateOptions(second, "gemma:4b", "v1"));
    }

    [Fact]
    public void Different_inputs_and_versions_have_distinct_keys()
    {
        var request = Request();
        var baseline = RecipeGenerationCacheKey.CreateOptions(request, "model-1", "prompt-1");

        request.ServingSize++;
        Assert.NotEqual(baseline, RecipeGenerationCacheKey.CreateOptions(request, "model-1", "prompt-1"));
        request.ServingSize--;
        request.DietaryRestrictions.Add("peanut allergy");
        Assert.NotEqual(baseline, RecipeGenerationCacheKey.CreateOptions(request, "model-1", "prompt-1"));
        request.DietaryRestrictions.Clear();
        request.TargetBudget++;
        Assert.NotEqual(baseline, RecipeGenerationCacheKey.CreateOptions(request, "model-1", "prompt-1"));
        request.TargetBudget--;
        request.CuisinePreference = "Thai";
        Assert.NotEqual(baseline, RecipeGenerationCacheKey.CreateOptions(request, "model-1", "prompt-1"));
        request.CuisinePreference = "Italian";
        Assert.NotEqual(baseline, RecipeGenerationCacheKey.CreateOptions(request, "model-2", "prompt-1"));
        Assert.NotEqual(baseline, RecipeGenerationCacheKey.CreateOptions(request, "model-1", "prompt-2"));
    }

    [Fact]
    public void Full_recipe_key_includes_selected_identity()
    {
        var request = Request();
        request.SelectedOption = Option("Bean stew");
        var first = RecipeGenerationCacheKey.CreateFull(request, "model", "prompt");
        request.SelectedOption = Option("Tomato soup");

        Assert.NotEqual(first, RecipeGenerationCacheKey.CreateFull(request, "model", "prompt"));
        Assert.NotEqual(first, RecipeGenerationCacheKey.CreateOptions(request, "model", "prompt"));
        request.SelectedOption = Option("Bean stew");
        request.SelectedOption.Ingredients.Add("onion");
        Assert.NotEqual(first, RecipeGenerationCacheKey.CreateFull(request, "model", "prompt"));
        request.SelectedOption.Ingredients.Remove("onion");
        request.SelectedOption.TotalCost = 12m;
        Assert.NotEqual(first, RecipeGenerationCacheKey.CreateFull(request, "model", "prompt"));
        request.SelectedOption.TotalCost = 0m;
        request.SelectedOption.Nutrition = new NutritionInfo { Calories = 350m };
        Assert.NotEqual(first, RecipeGenerationCacheKey.CreateFull(request, "model", "prompt"));
        request.SelectedOption.Nutrition = null;
        request.SelectedOption.Ingredients.Add("beans");
        Assert.NotEqual(first, RecipeGenerationCacheKey.CreateFull(request, "model", "prompt"));
    }

    [Fact]
    public async Task Options_hit_skips_generator_and_returns_isolated_copy()
    {
        var cache = new MemoryCache();
        var coordinator = CreateCoordinator(cache);
        var calls = 0;
        Task Generate(RecipeGenerationContext context)
        {
            calls++;
            context.ProposedOptions = [Option("Bean stew")];
            context.DataSource = RecipeDataSource.Real;
            return Task.CompletedTask;
        }

        var first = await coordinator.GetOptionsAsync(Request(), RecipeDataSource.Real, Generate);
        first.ProposedOptions[0].Ingredients.Add("changed");
        var second = await coordinator.GetOptionsAsync(Request(), RecipeDataSource.Real, Generate);

        Assert.Equal(1, calls);
        Assert.Equal(["beans"], second.ProposedOptions[0].Ingredients);
    }

    [Fact]
    public async Task Full_recipe_hit_skips_generator()
    {
        var coordinator = CreateCoordinator(new MemoryCache());
        var calls = 0;
        Task Generate(RecipeGenerationContext context)
        {
            calls++;
            context.FinalRecipe = new FullRecipe { Title = "Bean stew", Instructions = "Cook." };
            context.DataSource = RecipeDataSource.Real;
            return Task.CompletedTask;
        }

        var first = await coordinator.GetFullRecipeAsync(SelectedRequest(), RecipeDataSource.Real, Generate);
        first.FinalRecipe!.Instructions = "mutated";
        var second = await coordinator.GetFullRecipeAsync(SelectedRequest(), RecipeDataSource.Real, Generate);

        Assert.Equal(1, calls);
        Assert.Equal("Cook.", second.FinalRecipe!.Instructions);
    }

    [Fact]
    public async Task Live_request_promotes_synthetic_entry()
    {
        var cache = new MemoryCache();
        var coordinator = CreateCoordinator(cache);
        await coordinator.GetOptionsAsync(Request(), RecipeDataSource.Synthetic, context =>
        {
            var option = Option("Synthetic");
            option.DataSource = RecipeDataSource.Synthetic;
            context.ProposedOptions = [option];
            context.DataSource = RecipeDataSource.Synthetic;
            return Task.CompletedTask;
        });

        var real = await coordinator.GetOptionsAsync(Request(), RecipeDataSource.Real, context =>
        {
            context.ProposedOptions = [Option("Real")];
            context.DataSource = RecipeDataSource.Real;
            return Task.CompletedTask;
        });

        Assert.Equal("Real", real.ProposedOptions[0].Name);
        Assert.Equal(RecipeDataSource.Real, cache.Entry!.Source);
    }

    [Fact]
    public async Task Synthetic_request_reuses_real_entry()
    {
        var coordinator = CreateCoordinator(new MemoryCache());
        await coordinator.GetOptionsAsync(Request(), RecipeDataSource.Real, context =>
        {
            context.ProposedOptions = [Option("Real")];
            context.DataSource = RecipeDataSource.Real;
            return Task.CompletedTask;
        });

        var result = await coordinator.GetOptionsAsync(Request(), RecipeDataSource.Synthetic,
            _ => throw new InvalidOperationException("Generator should not run."));

        Assert.Equal("Real", result.ProposedOptions[0].Name);
    }

    [Fact]
    public async Task Lower_source_cannot_demote_expired_real_entry()
    {
        var clock = new ManualTimeProvider();
        var cache = new MemoryCache(clock);
        var coordinator = CreateCoordinator(cache, clock);
        await coordinator.GetOptionsAsync(Request(), RecipeDataSource.Real, context =>
        {
            context.ProposedOptions = [Option("Real")];
            context.DataSource = RecipeDataSource.Real;
            return Task.CompletedTask;
        });
        clock.Advance(TimeSpan.FromHours(2));

        var result = await coordinator.GetOptionsAsync(Request(), RecipeDataSource.Synthetic, context =>
        {
            var option = Option("Synthetic");
            option.DataSource = RecipeDataSource.Synthetic;
            context.ProposedOptions = [option];
            context.DataSource = RecipeDataSource.Synthetic;
            return Task.CompletedTask;
        });

        Assert.Equal("Synthetic", result.ProposedOptions[0].Name);
        Assert.Equal(RecipeDataSource.Real, cache.Entry!.Source);
    }

    [Fact]
    public async Task Failed_generation_is_not_cached()
    {
        var cache = new MemoryCache();
        var coordinator = CreateCoordinator(cache);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            coordinator.GetOptionsAsync(Request(), RecipeDataSource.Real,
                _ => throw new InvalidOperationException("failure")));

        Assert.Null(cache.Entry);
    }

    [Fact]
    public async Task Incomplete_generation_does_not_cache_stale_request_output()
    {
        var cache = new MemoryCache();
        var coordinator = CreateCoordinator(cache);
        var request = Request();
        request.ProposedOptions = [Option("Old")];

        var result = await coordinator.GetOptionsAsync(request, RecipeDataSource.Real,
            _ => Task.CompletedTask);

        Assert.Empty(result.ProposedOptions);
        Assert.Null(cache.Entry);
    }

    [Fact]
    public async Task Reused_context_clears_previous_phase_state_before_generation()
    {
        var coordinator = CreateCoordinator(new MemoryCache());
        var request = SelectedRequest();
        request.FinalRecipe = new FullRecipe { Instructions = "Old" };
        request.ProposedOptions = [Option("Old")];
        request.IsHandled = true;

        await coordinator.GetOptionsAsync(request, RecipeDataSource.Real, generated =>
        {
            Assert.False(generated.IsHandled);
            Assert.Null(generated.SelectedOption);
            Assert.Null(generated.FinalRecipe);
            Assert.Empty(generated.ProposedOptions);
            generated.ProposedOptions = [Option("New")];
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Blank_full_recipe_does_not_cache_stale_request_output()
    {
        var cache = new MemoryCache();
        var coordinator = CreateCoordinator(cache);
        var request = SelectedRequest();
        request.FinalRecipe = new FullRecipe { Instructions = "Old" };

        var result = await coordinator.GetFullRecipeAsync(request, RecipeDataSource.Real, context =>
        {
            context.FinalRecipe = new FullRecipe { Instructions = "   " };
            return Task.CompletedTask;
        });

        Assert.Equal("   ", result.FinalRecipe!.Instructions);
        Assert.Null(cache.Entry);
    }

    [Fact]
    public async Task Full_recipe_source_follows_selected_option()
    {
        var coordinator = CreateCoordinator(new MemoryCache());
        var request = SelectedRequest();
        request.DataSource = RecipeDataSource.Estimate;

        var result = await coordinator.GetFullRecipeAsync(request, RecipeDataSource.Real, context =>
        {
            context.FinalRecipe = new FullRecipe { Instructions = "Cook." };
            return Task.CompletedTask;
        });

        Assert.Equal(RecipeDataSource.Real, result.DataSource);
    }

    [Fact]
    public async Task Queued_cancellation_does_not_cache_or_block_later_request()
    {
        var coordinator = CreateCoordinator(new MemoryCache());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Generate(RecipeGenerationContext context)
        {
            started.SetResult();
            await release.Task;
            context.ProposedOptions = [Option("Bean stew")];
        }

        var first = coordinator.GetOptionsAsync(Request(), RecipeDataSource.Real, Generate);
        await started.Task;
        using var cancellation = new CancellationTokenSource();
        var queued = coordinator.GetOptionsAsync(Request(), RecipeDataSource.Real,
            _ => throw new InvalidOperationException("Should not run"), cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        release.SetResult();
        await first;
        var later = await coordinator.GetOptionsAsync(Request(), RecipeDataSource.Real,
            _ => throw new InvalidOperationException("Should not run"));

        Assert.Single(later.ProposedOptions);
    }

    [Fact]
    public async Task Concurrent_same_request_runs_generator_once()
    {
        var coordinator = CreateCoordinator(new MemoryCache());
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async Task Generate(RecipeGenerationContext context)
        {
            Interlocked.Increment(ref calls);
            started.SetResult();
            await release.Task;
            context.ProposedOptions = [Option("Bean stew")];
            context.DataSource = RecipeDataSource.Real;
        }

        var first = coordinator.GetOptionsAsync(Request(), RecipeDataSource.Real, Generate);
        await started.Task;
        var second = coordinator.GetOptionsAsync(Request(), RecipeDataSource.Real, Generate);
        release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, calls);
    }

    private static RecipeGenerationContext Request() => new()
    {
        ServingSize = 2,
        TargetBudget = 20m,
        CuisinePreference = "Italian"
    };

    private static RecipeGenerationContext SelectedRequest()
    {
        var request = Request();
        request.SelectedOption = Option("Bean stew");
        return request;
    }

    private static RecipeOption Option(string name) => new()
    {
        Name = name,
        Ingredients = ["beans"],
        DataSource = RecipeDataSource.Real
    };

    private static RecipeGenerationCoordinator CreateCoordinator(MemoryCache cache,
        TimeProvider? clock = null) => new(cache, clock ?? TimeProvider.System,
        TimeSpan.FromHours(1), "model", "prompt");

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class MemoryCache(TimeProvider? clock = null) : IRecipeGenerationCache
    {
        public RecipeGenerationCacheEntry? Entry { get; private set; }

        public Task<RecipeGenerationCacheEntry?> GetAsync(string key,
            CancellationToken cancellationToken = default)
        {
            var entry = Entry;
            if (entry?.Key != key || entry.ExpiresAtUtc <= (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime)
                entry = null;
            return Task.FromResult(entry);
        }

        public Task<RecipeGenerationCacheEntry> StoreAsync(RecipeGenerationCacheEntry entry,
            CancellationToken cancellationToken = default)
        {
            if (Entry is null || entry.Source >= Entry.Source)
                Entry = entry;
            return Task.FromResult(Entry);
        }
    }
}
