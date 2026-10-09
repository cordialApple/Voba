using Voba.Contracts;
using Voba.Interfaces;
using Voba.Models;
using Voba.Services;
using System.Globalization;
using System.Text.Json;

namespace Voba.Backend;

public sealed class RecipeWorkflowService
{
    private readonly IGenerationDraftStore _drafts;
    private readonly RecipeGenerationCoordinator _coordinator;
    private readonly IRecipeGenerator _generator;
    private readonly TimeProvider _clock;
    private readonly RecipeDataSource _minimumSource;
    private readonly TimeSpan _draftTtl;

    public RecipeWorkflowService(IGenerationDraftStore drafts,
        RecipeGenerationCoordinator coordinator, IRecipeGenerator generator,
        TimeProvider clock, RecipeDataSource minimumSource, TimeSpan draftTtl)
    {
        _drafts = drafts;
        _coordinator = coordinator;
        _generator = generator;
        _clock = clock;
        _minimumSource = minimumSource;
        _draftTtl = draftTtl > TimeSpan.Zero ? draftTtl : throw new ArgumentOutOfRangeException(nameof(draftTtl));
    }

    public async Task<GenerationOptionsResponse> CreateOptionsAsync(string userId,
        GenerationOptionsRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID required.", nameof(userId));
        if (request.Budget is <= 0 or > 1_000_000 ||
            request.Servings is <= 0 or > 1000 ||
            request.DietaryRestrictions is null || request.DietaryRestrictions.Count > 32 ||
            request.DietaryRestrictions.Any(value => value is null || value.Length > 200) ||
            request.CuisinePreference?.Length > 200)
            throw new ArgumentException("Budget, servings, and restrictions must be valid.", nameof(request));

        var context = new RecipeGenerationContext
        {
            TargetBudget = request.Budget,
            ServingSize = request.Servings,
            DietaryRestrictions = request.DietaryRestrictions
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()).ToList(),
            CuisinePreference = string.IsNullOrWhiteSpace(request.CuisinePreference)
                ? null : request.CuisinePreference.Trim()
        };
        var generated = await _coordinator.GetOptionsAsync(context, _minimumSource, async current =>
        {
            await _generator.GenerateOptionsAsync(current, cancellationToken);
            current.ProposedOptions = RecipeGenerationPolicy.WithinBudget(
                current.ProposedOptions, current.TargetBudget, current.ServingSize);
        }, cancellationToken);
        if (generated.ProposedOptions.Count == 0)
            throw new InvalidOperationException("No usable recipes returned.");

        cancellationToken.ThrowIfCancellationRequested();
        var draft = await _drafts.CreateAsync(userId, generated,
            _clock.GetUtcNow().UtcDateTime.Add(_draftTtl), cancellationToken);
        return new GenerationOptionsResponse(draft.Id, generated.ProposedOptions
            .Select((option, index) => MapOption(option, index)).ToArray());
    }

    public async Task<FullRecipeResponse?> SelectAsync(string userId, string draftId,
        SelectRecipeRequest request, CancellationToken cancellationToken = default)
    {
        var draft = await _drafts.GetAsync(draftId, userId, cancellationToken);
        if (draft is null)
            return null;
        if (!int.TryParse(request.OptionId, NumberStyles.None, CultureInfo.InvariantCulture,
                out var index) || index < 0 || index >= draft.Context.ProposedOptions.Count)
            throw new ArgumentOutOfRangeException(nameof(request), "Option ID is invalid.");

        var selected = JsonSerializer.Deserialize<RecipeGenerationContext>(
            JsonSerializer.Serialize(draft.Context))!;
        selected.SelectedOption = selected.ProposedOptions[index];
        selected.FinalRecipe = null;
        var full = await _coordinator.GetFullRecipeAsync(selected, _minimumSource,
            current => _generator.GenerateFullAsync(current, cancellationToken), cancellationToken);
        if (string.IsNullOrWhiteSpace(full.FinalRecipe?.Instructions))
            throw new InvalidOperationException("No cooking instructions returned.");
        cancellationToken.ThrowIfCancellationRequested();
        if (!await _drafts.ReplaceAsync(draft, full, cancellationToken))
            throw new DraftConflictException();

        return new FullRecipeResponse(draft.Id, draft.Version + 1, full.FinalRecipe.Title,
            full.FinalRecipe.Instructions, MapOption(full.SelectedOption!, index),
            full.ServingSize, full.TargetBudget, full.DietaryRestrictions,
            full.CuisinePreference);
    }

    private static RecipeOptionResponse MapOption(RecipeOption option, int index) => new(
        index.ToString(CultureInfo.InvariantCulture), option.Name, option.Ingredients,
        option.EstimatedCost, option.TotalCost, MapNutrition(option.Nutrition),
        option.DataSource.ToString(), option.NutritionSource.ToString());

    private static NutritionResponse? MapNutrition(NutritionInfo? nutrition) =>
        nutrition is null || !nutrition.HasData ? null : new NutritionResponse(
            nutrition.Calories, nutrition.ProteinGrams, nutrition.FatGrams, nutrition.CarbGrams);
}

public sealed class DraftConflictException : Exception;
