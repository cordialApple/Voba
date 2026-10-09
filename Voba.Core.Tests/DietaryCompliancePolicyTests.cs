using Voba.Models;
using Voba.Services;
using Xunit;

namespace Voba.Core.Tests;

public sealed class DietaryCompliancePolicyTests
{
    [Theory]
    [InlineData("chicken breast")]
    [InlineData("ground beef")]
    [InlineData("salmon fillet")]
    [InlineData("shrimp")]
    [InlineData("fish sauce")]
    [InlineData("parmesan cheese")]
    [InlineData("whole milk")]
    [InlineData("eggs")]
    [InlineData("honey")]
    [InlineData("gelatin")]
    [InlineData("sausages")]
    [InlineData("steaks")]
    [InlineData("beef burgers")]
    [InlineData("buttermilk")]
    public void Vegan_rejects_common_animal_ingredients(string ingredient)
    {
        Assert.False(DietaryCompliancePolicy.AllowsOption(
            Option(ingredient), ["vegan"]));
    }

    [Theory]
    [InlineData("chicken breast")]
    [InlineData("ground beef")]
    [InlineData("salmon fillet")]
    [InlineData("shrimp")]
    [InlineData("fish sauce")]
    [InlineData("sausages")]
    [InlineData("steaks")]
    public void Vegetarian_rejects_meat_fish_and_seafood(string ingredient)
    {
        Assert.False(DietaryCompliancePolicy.AllowsOption(
            Option(ingredient), ["vegetarian"]));
    }

    [Theory]
    [InlineData("coconut milk")]
    [InlineData("oat milk")]
    [InlineData("vegan cheese")]
    [InlineData("vegan butter")]
    [InlineData("peanut butter")]
    [InlineData("butter beans")]
    [InlineData("eggplant")]
    [InlineData("tofu")]
    [InlineData("cocoa butter")]
    [InlineData("cream of tartar")]
    [InlineData("veggie burger")]
    [InlineData("vegan sausages")]
    public void Vegan_allows_common_plant_alternatives(string ingredient)
    {
        Assert.True(DietaryCompliancePolicy.AllowsOption(
            Option(ingredient), ["vegan"]));
    }

    [Fact]
    public void Vegetarian_allows_eggs_and_dairy()
    {
        Assert.True(DietaryCompliancePolicy.AllowsOption(
            Option("eggs", "cheddar cheese"), ["vegetarian"]));
    }

    [Fact]
    public void Other_restrictions_do_not_trigger_vegan_or_vegetarian_filter()
    {
        Assert.True(DietaryCompliancePolicy.AllowsOption(
            Option("chicken breast"), ["gluten-free"]));
    }

    [Theory]
    [InlineData("1. Stir in butter and serve.")]
    [InlineData("1. Add fish sauce after cooking.")]
    [InlineData("1. Top with grated parmesan cheese.")]
    public void Vegan_rejects_forbidden_instruction_additions(string instructions)
    {
        Assert.False(DietaryCompliancePolicy.AllowsFullRecipe(
            Option("tofu", "rice"), instructions, ["vegan"]));
    }

    [Fact]
    public void Vegan_accepts_plant_alternative_instructions()
    {
        Assert.True(DietaryCompliancePolicy.AllowsFullRecipe(
            Option("tofu", "coconut milk"),
            "1. Simmer tofu in coconut milk. 2. Add peanut butter.", ["vegan"]));
    }

    private static RecipeOption Option(params string[] ingredients) => new()
    {
        Name = "Dinner",
        Ingredients = ingredients.ToList()
    };
}
