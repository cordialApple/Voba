using System.Text.Json;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Services;

public sealed class RecipeGenerationCoordinator
{
    private readonly IRecipeGenerationCache _cache;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _ttl;
    private readonly string _modelVersion;
    private readonly string _promptVersion;
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, 64)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public RecipeGenerationCoordinator(IRecipeGenerationCache cache, TimeProvider clock,
        TimeSpan ttl, string modelVersion, string promptVersion)
    {
        _cache = cache;
        _clock = clock;
        _ttl = ttl > TimeSpan.Zero ? ttl : throw new ArgumentOutOfRangeException(nameof(ttl));
        _modelVersion = modelVersion;
        _promptVersion = promptVersion;
    }

    public Task<RecipeGenerationContext> GetOptionsAsync(RecipeGenerationContext request,
        RecipeDataSource minimumSource, Func<RecipeGenerationContext, Task> generate,
        CancellationToken cancellationToken = default) => GetAsync(
            RecipeGenerationCacheKey.CreateOptions(request, _modelVersion, _promptVersion),
            request, minimumSource, generate,
            result => result.ProposedOptions.Count > 0, false, cancellationToken);

    public Task<RecipeGenerationContext> GetFullRecipeAsync(RecipeGenerationContext request,
        RecipeDataSource minimumSource, Func<RecipeGenerationContext, Task> generate,
        CancellationToken cancellationToken = default) => GetAsync(
            RecipeGenerationCacheKey.CreateFull(request, _modelVersion, _promptVersion),
            request, minimumSource, generate,
            result => !string.IsNullOrWhiteSpace(result.FinalRecipe?.Instructions), true, cancellationToken);

    private async Task<RecipeGenerationContext> GetAsync(string key,
        RecipeGenerationContext request, RecipeDataSource minimumSource,
        Func<RecipeGenerationContext, Task> generate,
        Func<RecipeGenerationContext, bool> isComplete, bool full,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cached = await GetUsableAsync(key, minimumSource, cancellationToken);
        if (cached is not null)
            return cached;

        var gate = _gates[(int)((uint)key.GetHashCode(StringComparison.Ordinal) % (uint)_gates.Length)];
        await gate.WaitAsync(cancellationToken);
        try
        {
            cached = await GetUsableAsync(key, minimumSource, cancellationToken);
            if (cached is not null)
                return cached;

            var result = Copy(request);
            result.IsHandled = false;
            if (full)
                result.FinalRecipe = null;
            else
            {
                result.ProposedOptions.Clear();
                result.SelectedOption = null;
                result.FinalRecipe = null;
            }
            await generate(result);
            cancellationToken.ThrowIfCancellationRequested();
            if (!isComplete(result))
                return result;

            result.DataSource = full
                ? result.SelectedOption!.DataSource
                : result.ProposedOptions.Min(option => option.DataSource);

            var now = _clock.GetUtcNow().UtcDateTime;
            var entry = new RecipeGenerationCacheEntry(key, result.DataSource,
                JsonSerializer.Serialize(result), now, now.Add(_ttl));
            var winner = await _cache.StoreAsync(entry, cancellationToken);
            if (winner.Source >= minimumSource && winner.ExpiresAtUtc > now)
                return JsonSerializer.Deserialize<RecipeGenerationContext>(winner.PayloadJson)!;
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<RecipeGenerationContext?> GetUsableAsync(string key,
        RecipeDataSource minimumSource, CancellationToken cancellationToken)
    {
        var entry = await _cache.GetAsync(key, cancellationToken);
        if (entry is null || entry.Source < minimumSource ||
            entry.ExpiresAtUtc <= _clock.GetUtcNow().UtcDateTime)
            return null;
        return JsonSerializer.Deserialize<RecipeGenerationContext>(entry.PayloadJson);
    }

    private static RecipeGenerationContext Copy(RecipeGenerationContext context) =>
        JsonSerializer.Deserialize<RecipeGenerationContext>(JsonSerializer.Serialize(context))!;
}
