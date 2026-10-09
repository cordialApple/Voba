using System.Text.RegularExpressions;
using Voba.Models;

namespace Voba.Services;

public static class DietaryCompliancePolicy
{
    private enum DietMode { None, Vegetarian, Vegan }

    private static readonly Regex PlantAlternatives = Terms(
        "coconut milk", "oat milk", "almond milk", "soy milk", "cashew milk",
        "rice milk", "plant milk", "vegan milk", "vegan cheese", "vegan butter",
        "vegan cream", "vegan yogurt", "vegan mayonnaise", "vegan mayo",
        "peanut butter", "almond butter", "cashew butter", "sunflower butter",
        "butter beans", "cocoa butter", "cream of tartar", "oyster mushrooms",
        "oyster mushroom", "vegan fish sauce", "veggie burger", "vegan burger",
        "plant-based burger", "plant based burger", "vegan sausage", "vegan sausages",
        "plant-based sausage", "plant based sausage");

    private static readonly Regex MeatFishSeafood = Terms(
        "meat", "poultry", "beef", "steak", "steaks", "pork", "bacon", "ham",
        "sausage", "sausages", "burger", "burgers", "hamburger", "hamburgers",
        "chicken", "turkey", "duck", "lamb", "mutton", "veal", "lard", "tallow",
        "fish", "salmon", "tuna", "cod", "anchovy", "anchovies", "sardine",
        "sardines", "shrimp", "prawn", "prawns", "crab", "lobster", "oyster",
        "oysters", "clam", "clams", "mussel", "mussels", "scallop", "scallops",
        "squid", "octopus", "seafood", "gelatin", "gelatine");

    private static readonly Regex OtherAnimalProducts = Terms(
        "milk", "buttermilk", "cheese", "butter", "yogurt", "yoghurt", "cream", "ghee",
        "whey", "casein", "lactose", "parmesan", "mozzarella", "cheddar",
        "feta", "paneer", "ricotta", "egg", "eggs", "mayonnaise", "mayo",
        "honey", "beeswax");

    public static bool AllowsOption(RecipeOption option, IEnumerable<string> restrictions) =>
        AllowsOption(option, Classify(restrictions));

    private static bool AllowsOption(RecipeOption option, DietMode diet)
    {
        if (diet == DietMode.None)
            return true;
        if (option.Ingredients is null || option.Ingredients.Count == 0)
            return false;
        return !HasProhibitedIngredient(option.Name, diet) &&
            option.Ingredients.All(ingredient => !HasProhibitedIngredient(ingredient, diet));
    }

    public static bool AllowsFullRecipe(RecipeOption option, string? instructions,
        IEnumerable<string> restrictions)
    {
        var diet = Classify(restrictions);
        return diet == DietMode.None ||
            AllowsOption(option, diet) &&
            !string.IsNullOrWhiteSpace(instructions) &&
            !HasProhibitedIngredient(instructions, diet);
    }

    private static DietMode Classify(IEnumerable<string> restrictions)
    {
        var names = restrictions.Select(value => value.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (names.Contains("vegan")) return DietMode.Vegan;
        if (names.Contains("vegetarian")) return DietMode.Vegetarian;
        return DietMode.None;
    }

    private static bool HasProhibitedIngredient(string? value, DietMode diet)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var normalized = PlantAlternatives.Replace(value, " ");
        return MeatFishSeafood.IsMatch(normalized) ||
            diet == DietMode.Vegan && OtherAnimalProducts.IsMatch(normalized);
    }

    private static Regex Terms(params string[] terms) => new(
        $@"(?<!\p{{L}})(?:{string.Join("|", terms.Select(Regex.Escape))})(?!\p{{L}})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
}
