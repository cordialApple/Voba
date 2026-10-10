using Voba.Models;
using Voba.Interfaces;
using Voba.Services;
using Xunit;

namespace Voba.Core.Tests;

public class RecipeGenerationPolicyTests
{
    [Theory]
    [InlineData("20", "2", true)]
    [InlineData("0", "2", false)]
    [InlineData("-3", "2", false)]
    [InlineData("abc", "2", false)]
    [InlineData("20", "0", false)]
    [InlineData("20", "-1", false)]
    [InlineData("20", "two", false)]
    public void Requires_positive_budget_and_servings(string budget, string servings, bool expected)
    {
        Assert.Equal(expected, RecipeGenerationPolicy.TryParseInputs(budget, servings, out _, out _));
    }

    [Fact]
    public void Filters_options_by_total_cost()
    {
        var options = new[]
        {
            new RecipeOption { Name = "Within", EstimatedCost = 5m, TotalCost = 10m },
            new RecipeOption { Name = "Over", EstimatedCost = 6m, TotalCost = 12m },
            new RecipeOption { Name = "Derived", EstimatedCost = 4m },
            new RecipeOption { Name = "Unknown" }
        };

        var accepted = RecipeGenerationPolicy.WithinBudget(options, 10m, 2);

        Assert.Equal(["Within", "Derived"], accepted.Select(option => option.Name));
        Assert.Equal(8m, accepted[1].TotalCost);
    }

    [Fact]
    public void Model_source_claim_is_reset()
    {
        var option = new RecipeOption
        {
            EstimatedCost = 3m,
            TotalCost = 5m,
            Nutrition = new NutritionInfo { Calories = 900m },
            DataSource = RecipeDataSource.Real
        };

        RecipeGenerationPolicy.ResetModelSources([option]);

        Assert.Equal(RecipeDataSource.Estimate, option.DataSource);
        Assert.Equal(3m, option.EstimatedCost);
        Assert.Equal(0m, option.TotalCost);
        Assert.Null(option.Nutrition);
        Assert.Equal(RecipeDataSource.Estimate, option.NutritionSource);
    }

    [Fact]
    public void Missing_enrichment_keeps_model_estimate_source()
    {
        var option = new RecipeOption { EstimatedCost = 3m, DataSource = RecipeDataSource.Real };

        RecipeGenerationPolicy.ApplyEnrichment(option, null, 2);

        Assert.Equal(RecipeDataSource.Estimate, option.DataSource);
        Assert.Equal(6m, option.TotalCost);
    }

    [Fact]
    public void Priced_enrichment_uses_provider_source()
    {
        var option = new RecipeOption { EstimatedCost = 3m };
        var enrichment = new RecipeEnrichment(4m, 8m,
            new NutritionInfo { Calories = 250m }, RecipeDataSource.Synthetic);

        RecipeGenerationPolicy.ApplyEnrichment(option, enrichment, 2);

        Assert.Equal(RecipeDataSource.Synthetic, option.DataSource);
        Assert.Equal(8m, option.TotalCost);
        Assert.Equal(250m, option.Nutrition!.Calories);
        Assert.Equal(RecipeDataSource.Synthetic, option.NutritionSource);
    }

    [Fact]
    public async Task Provider_failure_falls_back_to_model_estimate()
    {
        var option = new RecipeOption { EstimatedCost = 3m, DataSource = RecipeDataSource.Real };

        await RecipeGenerationPolicy.EnrichAsync(option, 2, new FailingProvider());

        Assert.Equal(RecipeDataSource.Estimate, option.DataSource);
        Assert.Equal(6m, option.TotalCost);
    }

    private sealed class FailingProvider : IRecipeEnrichmentService
    {
        public Task<RecipeEnrichment?> EnrichAsync(IEnumerable<string> ingredients, int servings) =>
            Task.FromException<RecipeEnrichment?>(new InvalidOperationException("provider offline"));
    }
}
