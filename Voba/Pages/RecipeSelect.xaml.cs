using Voba.AI.Pipeline.Handlers;
using Voba.Models;

namespace Voba.Pages;

[QueryProperty(nameof(Context), "Context")]
public partial class RecipeSelect : ContentPage
{
    private readonly GemmaFullRecipeHandler _fullRecipeHandler;
    private RecipeGenerationContext? _context;

    // Shell passes the context object here via QueryProperty after Forum navigates
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

    // Populate cards from context

    private void PopulateCards(RecipeGenerationContext context)
    {
        var options = context.ProposedOptions;

        if (options.Count >= 1) FillCard(1, options[0]);
        if (options.Count >= 2) FillCard(2, options[1]);
    }

    private void FillCard(int number, RecipeOption recipe)
    {
        if (number == 1)
        {
            Card1Title.Text = recipe.Name;
            Card1Ingredients.Text = string.Join(", ", recipe.Ingredients);
            Card1Cost.Text = $"${recipe.EstimatedCost:F2}";
            Card1TotalCost.Text = $"${recipe.TotalCost:F2}";
            Card1.IsVisible = true;
        }
        else
        {
            Card2Title.Text = recipe.Name;
            Card2Ingredients.Text = string.Join(", ", recipe.Ingredients);
            Card2Cost.Text = $"${recipe.EstimatedCost:F2}";
            Card2TotalCost.Text = $"${recipe.TotalCost:F2}";
            Card2.IsVisible = true;
        }
    }

    // Select handlers 

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

        // Disable both buttons and show loading
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
        await Shell.Current.GoToAsync($"//{nameof(Home)}");
    }
}