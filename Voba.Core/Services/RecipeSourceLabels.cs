using Voba.Models;

namespace Voba.Services;

public static class RecipeSourceLabels
{
    public static string ForOption(RecipeOption option)
        => Format(option.DataSource, option.NutritionSource,
            option.Nutrition?.HasData == true);

    public static string ForRecipe(Recipe recipe)
        => Format(recipe.DataSource, recipe.NutritionSource,
            recipe.Nutrition?.HasData == true);

    private static string Format(RecipeDataSource costSource,
        RecipeDataSource nutritionSource, bool hasNutrition)
    {
        var cost = costSource switch
        {
            RecipeDataSource.Real => "Spoonacular live",
            RecipeDataSource.Synthetic => "demo data",
            _ => "Gemma estimate"
        };
        var nutrition = hasNutrition
            ? nutritionSource switch
            {
                RecipeDataSource.Real => "Spoonacular live",
                RecipeDataSource.Synthetic => "demo data",
                _ => "estimate"
            }
            : "unavailable";
        return $"Cost: {cost} · Nutrition: {nutrition}";
    }
}
