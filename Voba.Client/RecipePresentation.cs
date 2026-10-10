namespace Voba.Client;

public static class RecipePresentation
{
    public static string SourceLabel(string costSource, string nutritionSource,
        bool hasNutrition)
    {
        var cost = costSource switch
        {
            "Real" => "Spoonacular live",
            "Synthetic" => "demo data",
            _ => "Gemma estimate"
        };
        var nutrition = hasNutrition
            ? nutritionSource switch
            {
                "Real" => "Spoonacular live",
                "Synthetic" => "demo data",
                _ => "estimate"
            }
            : "unavailable";
        return $"Cost: {cost} · Nutrition: {nutrition}";
    }
}
