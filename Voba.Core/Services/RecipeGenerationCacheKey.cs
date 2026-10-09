using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Voba.Models;

namespace Voba.Services;

public static class RecipeGenerationCacheKey
{
    public static string CreateOptions(RecipeGenerationContext context, string modelVersion, string promptVersion) =>
        Create(context, modelVersion, promptVersion, "options", null);

    public static string CreateFull(RecipeGenerationContext context, string modelVersion, string promptVersion)
    {
        var option = context.SelectedOption ?? throw new InvalidOperationException("No recipe selected.");
        var nutrition = option.Nutrition;
        var selected = new
        {
            Name = Normalize(option.Name),
            Ingredients = NormalizeList(option.Ingredients),
            EstimatedCost = Number(option.EstimatedCost),
            TotalCost = Number(option.TotalCost),
            Nutrition = nutrition is null ? null : new[]
            {
                Number(nutrition.Calories), Number(nutrition.ProteinGrams),
                Number(nutrition.FatGrams), Number(nutrition.CarbGrams)
            }
        };
        return Create(context, modelVersion, promptVersion, "full", selected);
    }

    private static string Create(RecipeGenerationContext context, string modelVersion,
        string promptVersion, string phase, object? selected)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            Phase = phase,
            Model = modelVersion.Trim(),
            Prompt = promptVersion.Trim(),
            Servings = context.ServingSize,
            Budget = Number(context.TargetBudget),
            Cuisine = Normalize(context.CuisinePreference),
            Restrictions = NormalizeSet(context.DietaryRestrictions),
            Selected = selected
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string[] NormalizeSet(IEnumerable<string> values) => NormalizeList(values)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static string[] NormalizeList(IEnumerable<string> values) => values
        .Select(Normalize)
        .Where(value => value.Length > 0)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    private static string Normalize(string? value) => string.Join(' ',
        (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        .ToLowerInvariant();

    private static string Number(decimal value) => value.ToString("G29", CultureInfo.InvariantCulture);
}
