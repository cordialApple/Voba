using Voba.AI.Pipeline.Handlers;
using Voba.Models;

namespace Voba.Pages;

[QueryProperty(nameof(Context), "Context")]
public partial class RecipeSelect : ContentPage
{
    private readonly GemmaFullRecipeHandler _fullRecipeHandler;
    private RecipeGenerationContext? _context;

    public RecipeGenerationContext? Context
    {
        set
        {
            _context = value;
            if (_context != null)
                PopulateCards(_context);
        }
    }

    public RecipeSelect(GemmaFullRecipeHandler fullRecipeHandler)
    {
        InitializeComponent();
        _fullRecipeHandler = fullRecipeHandler;
    }

    private void PopulateCards(RecipeGenerationContext context)
    {
        var options = context.ProposedOptions;

        if (options.Count >= 1) FillCard(1, options[0], context.ServingSize);
        if (options.Count >= 2) FillCard(2, options[1], context.ServingSize);
    }

    private void FillCard(int number, RecipeOption recipe, int servingSize)
    {
        // Derive TotalCost from Gemma's EstimatedCost if Spoonacular didn't price it
        if (recipe.TotalCost == 0 && recipe.EstimatedCost > 0)
            recipe.TotalCost = Math.Round(recipe.EstimatedCost * servingSize, 2);

        string perServing = recipe.EstimatedCost > 0 ? $"${recipe.EstimatedCost:F2}" : "—";
        string total = recipe.TotalCost > 0 ? $"${recipe.TotalCost:F2}" : "—";

        if (number == 1)
        {
            Card1Title.Text = recipe.Name;
            Card1Ingredients.Text = string.Join(", ", recipe.Ingredients);
            Card1Cost.Text = perServing;
            Card1TotalCost.Text = total;
            Card1.IsVisible = true;
        }
        else
        {
            Card2Title.Text = recipe.Name;
            Card2Ingredients.Text = string.Join(", ", recipe.Ingredients);
            Card2Cost.Text = perServing;
            Card2TotalCost.Text = total;
            Card2.IsVisible = true;
        }
    }

    private async void OnSelectRecipe1Clicked(object sender, EventArgs e)
    {
        if (_context?.ProposedOptions.Count >= 1)
            await GenerateAndNavigate(_context.ProposedOptions[0]);
    }

    private async void OnSelectRecipe2Clicked(object sender, EventArgs e)
    {
        if (_context?.ProposedOptions.Count >= 2)
            await GenerateAndNavigate(_context.ProposedOptions[1]);
    }

    private async Task GenerateAndNavigate(RecipeOption selected)
    {
        if (_context == null) return;

        _context.SelectedOption = selected;
        _context.IsHandled = false;
        _context.FinalRecipe = null;

        Card1.IsEnabled = false;
        Card2.IsEnabled = false;
        LoadingPanel.IsVisible = true;

        try
        {
            await _fullRecipeHandler.HandleAsync(_context);

            await Shell.Current.GoToAsync(nameof(Recipe),
                new Dictionary<string, object> { ["Context"] = _context });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            LoadingPanel.IsVisible = false;
            Card1.IsEnabled = true;
            Card2.IsEnabled = true;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(Home));
    }
}