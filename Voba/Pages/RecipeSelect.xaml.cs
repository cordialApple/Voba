using Voba.Client;
using Voba.Contracts;

namespace Voba.Pages;

[QueryProperty(nameof(Options), "Options")]
[QueryProperty(nameof(Servings), "Servings")]
public partial class RecipeSelect : ContentPage
{
    private readonly IVobaApiClient _api;
    private GenerationOptionsResponse? _options;
    private int _servings;

    public GenerationOptionsResponse? Options
    {
        set
        {
            _options = value;
            if (value is not null)
                PopulateCards(value);
        }
    }

    public int Servings
    {
        set
        {
            _servings = value;
            if (_options is not null)
                PopulateCards(_options);
        }
    }

    public RecipeSelect(IVobaApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    private void PopulateCards(GenerationOptionsResponse options)
    {
        Card1.IsVisible = false;
        Card2.IsVisible = false;
        if (options.Options.Count >= 1)
            FillCard(1, options.Options[0]);
        if (options.Options.Count >= 2)
            FillCard(2, options.Options[1]);
    }

    private void FillCard(int number, RecipeOptionResponse recipe)
    {
        var costPerServing = _servings > 0 && recipe.TotalCost > 0
            ? Math.Round(recipe.TotalCost / _servings, 2)
            : recipe.EstimatedCost;
        var perServing = costPerServing > 0 ? $"${costPerServing:F2}" : "—";
        var total = recipe.TotalCost > 0 ? $"${recipe.TotalCost:F2}" : "—";
        var source = RecipePresentation.SourceLabel(recipe.CostSource,
            recipe.NutritionSource, recipe.Nutrition is not null);

        if (number == 1)
        {
            Card1Title.Text = recipe.Name;
            Card1Ingredients.Text = string.Join(", ", recipe.Ingredients);
            Card1Cost.Text = perServing;
            Card1TotalCost.Text = total;
            Card1Source.Text = source;
            Card1.IsVisible = true;
        }
        else
        {
            Card2Title.Text = recipe.Name;
            Card2Ingredients.Text = string.Join(", ", recipe.Ingredients);
            Card2Cost.Text = perServing;
            Card2TotalCost.Text = total;
            Card2Source.Text = source;
            Card2.IsVisible = true;
        }
    }

    private async void OnSelectRecipe1Clicked(object sender, EventArgs e)
    {
        if (_options?.Options.Count >= 1)
            await GenerateAndNavigate(_options.Options[0]);
    }

    private async void OnSelectRecipe2Clicked(object sender, EventArgs e)
    {
        if (_options?.Options.Count >= 2)
            await GenerateAndNavigate(_options.Options[1]);
    }

    private async Task GenerateAndNavigate(RecipeOptionResponse selected)
    {
        if (_options is null)
            return;
        Card1.IsEnabled = false;
        Card2.IsEnabled = false;
        LoadingPanel.IsVisible = true;

        try
        {
            var full = await _api.SelectAsync(_options.DraftId, selected.OptionId);
            if (string.IsNullOrWhiteSpace(full.Instructions))
                throw new InvalidOperationException("No cooking instructions returned. Try again.");
            await Shell.Current.GoToAsync(nameof(Recipe),
                new Dictionary<string, object> { ["FullRecipe"] = full });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Recipe unavailable", ex.Message, "OK");
        }
        finally
        {
            LoadingPanel.IsVisible = false;
            Card1.IsEnabled = true;
            Card2.IsEnabled = true;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(Home));
}
