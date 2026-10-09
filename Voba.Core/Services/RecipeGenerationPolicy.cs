using Voba.Models;
using Voba.Interfaces;

namespace Voba.Services;

public static class RecipeGenerationPolicy
{
    public static async Task EnrichAsync(RecipeOption option, int servings,
        IRecipeEnrichmentService provider)
    {
        RecipeEnrichment? enrichment;
        try
        {
            enrichment = await provider.EnrichAsync(option.Ingredients, servings);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            enrichment = null;
        }
        ApplyEnrichment(option, enrichment, servings);
    }

    public static void ResetModelSources(IEnumerable<RecipeOption> options)
    {
        foreach (var option in options)
        {
            option.DataSource = RecipeDataSource.Estimate;
            option.NutritionSource = RecipeDataSource.Estimate;
            option.TotalCost = 0m;
            option.Nutrition = null;
        }
    }

    public static void ApplyEnrichment(RecipeOption option, RecipeEnrichment? enrichment, int servings)
    {
        option.DataSource = RecipeDataSource.Estimate;
        option.NutritionSource = RecipeDataSource.Estimate;
        if (enrichment is not null)
        {
            if (enrichment.TotalCostUsd > 0)
            {
                option.EstimatedCost = enrichment.CostPerServingUsd;
                option.TotalCost = enrichment.TotalCostUsd;
                option.DataSource = enrichment.Source;
            }
            if (enrichment.Nutrition.HasData)
            {
                option.Nutrition = enrichment.Nutrition;
                option.NutritionSource = enrichment.Source;
            }
        }
        if (option.TotalCost <= 0 && option.EstimatedCost > 0)
            option.TotalCost = Math.Round(option.EstimatedCost * Math.Max(servings, 1), 2);
    }

    public static bool TryParseInputs(string? budgetText, string? servingsText,
        out decimal budget, out int servings)
    {
        var validBudget = decimal.TryParse(budgetText, out budget) && budget > 0;
        var validServings = int.TryParse(servingsText, out servings) && servings > 0;
        return validBudget && validServings;
    }

    public static List<RecipeOption> WithinBudget(IEnumerable<RecipeOption> options,
        decimal budget, int servings)
    {
        var accepted = new List<RecipeOption>();
        foreach (var option in options)
        {
            if (option.TotalCost <= 0 && option.EstimatedCost > 0)
                option.TotalCost = Math.Round(option.EstimatedCost * servings, 2);
            if (option.TotalCost > 0 && option.TotalCost <= budget)
                accepted.Add(option);
        }
        return accepted;
    }
}
