using Voba.Models;
using Voba.Services;
using Xunit;

namespace Voba.Core.Tests;

public class RecipeSourceLabelsTests
{
    [Fact]
    public void Distinguishes_model_cost_from_live_nutrition()
    {
        var option = new RecipeOption
        {
            DataSource = RecipeDataSource.Estimate,
            NutritionSource = RecipeDataSource.Real,
            Nutrition = new NutritionInfo { Calories = 100m }
        };

        Assert.Equal("Cost: Gemma estimate · Nutrition: Spoonacular live",
            RecipeSourceLabels.ForOption(option));
    }

    [Fact]
    public void Missing_nutrition_is_explicit()
    {
        var option = new RecipeOption { DataSource = RecipeDataSource.Synthetic };

        Assert.Equal("Cost: demo data · Nutrition: unavailable",
            RecipeSourceLabels.ForOption(option));
    }

    [Fact]
    public void Saved_recipe_uses_persisted_sources()
    {
        var recipe = new Recipe("507f1f77bcf86cd799439011", "Soup", [], 8m, "Cook",
            new NutritionInfo { Calories = 150m }, RecipeDataSource.Real,
            RecipeDataSource.Synthetic);

        Assert.Equal("Cost: Spoonacular live · Nutrition: demo data",
            RecipeSourceLabels.ForRecipe(recipe));
    }
}
