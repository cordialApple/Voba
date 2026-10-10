using Microsoft.Maui.Controls.Shapes;
using Voba.Client;
using Voba.Contracts;

namespace Voba.Pages;

public partial class SavedRecipes : ContentPage
{
    private readonly IVobaApiClient? _api;

    public SavedRecipes(IVobaApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    public SavedRecipes()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadRecipesAsync();
    }

    private async Task LoadRecipesAsync()
    {
        if (_api is null)
        {
            EmptyState.IsVisible = true;
            return;
        }

        try
        {
            var recipes = await _api.ListAsync();
            RecipesLayout.Children.Clear();
            LoadErrorLabel.IsVisible = false;
            EmptyState.IsVisible = recipes.Count == 0;
            foreach (var recipe in recipes)
                RecipesLayout.Add(BuildRecipeCard(recipe));
        }
        catch
        {
            EmptyState.IsVisible = false;
            LoadErrorLabel.IsVisible = true;
        }
    }

    private Border BuildRecipeCard(SavedRecipeResponse recipe)
    {
        var stack = new VerticalStackLayout { Spacing = 4 };

        stack.Add(new Label
        {
            Text = recipe.Title,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2e4d2c")
        });

        var meta = new List<string>();
        if (recipe.TotalCost > 0)
            meta.Add($"${recipe.TotalCost:F2}");
        if (recipe.Nutrition is { } nutrition)
            meta.Add($"{nutrition.Calories:0} kcal");
        meta.Add($"{recipe.Ingredients.Count} ingredients");
        meta.Add(RecipePresentation.SourceLabel(recipe.CostSource, recipe.NutritionSource,
            recipe.Nutrition is not null));

        stack.Add(new Label
        {
            Text = string.Join("  ·  ", meta),
            FontSize = 12,
            TextColor = Color.FromArgb("#8a8078")
        });

        var card = new Border
        {
            BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Stroke = Color.FromArgb("#e0d9ce"),
            StrokeThickness = 1,
            Padding = new Thickness(20, 16),
            Content = stack
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OpenRecipeAsync(recipe.Id);
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private async Task OpenRecipeAsync(string recipeId)
    {
        if (_api is null)
            return;

        try
        {
            var recipe = await _api.GetAsync(recipeId);
            await Shell.Current.GoToAsync(nameof(Recipe), new Dictionary<string, object>
            {
                ["SavedRecipe"] = recipe
            });
        }
        catch (Exception ex)
        {
            await DisplayAlert("Recipe unavailable", ex.Message, "OK");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(Home));
    }
}
