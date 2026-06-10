using Microsoft.Maui.Controls.Shapes;
using Voba.Interfaces;
using Voba.Models;
using Voba.Services;

// Disambiguate from the Voba.Pages.Recipe ContentPage in this same namespace.
using ModelRecipe = Voba.Models.Recipe;

namespace Voba.Pages;

public partial class SavedRecipes : ContentPage
{
    private readonly IRecipeRepository? _recipeRepository;
    private readonly ICurrentUserService? _currentUser;

    // Runtime constructor — resolved by DI when navigating to the page.
    public SavedRecipes(IRecipeRepository recipeRepository, ICurrentUserService currentUser)
    {
        InitializeComponent();
        _recipeRepository = recipeRepository;
        _currentUser = currentUser;
    }

    // Parameterless constructor — used by the XAML designer/previewer only.
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
        RecipesLayout.Children.Clear();

        if (_recipeRepository is null || _currentUser is null ||
            !_currentUser.IsAuthenticated || string.IsNullOrEmpty(_currentUser.UserId))
        {
            EmptyState.IsVisible = true;
            return;
        }

        List<ModelRecipe> recipes;
        try
        {
            recipes = await _recipeRepository.GetByUserIdAsync(_currentUser.UserId);
        }
        catch
        {
            EmptyState.IsVisible = true;
            return;
        }

        if (recipes.Count == 0)
        {
            EmptyState.IsVisible = true;
            return;
        }

        EmptyState.IsVisible = false;
        foreach (var recipe in recipes)
            RecipesLayout.Add(BuildRecipeCard(recipe));
    }

    private static Border BuildRecipeCard(ModelRecipe recipe)
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
        if (recipe.EstimatedCost > 0)
            meta.Add($"${recipe.EstimatedCost:F2}");
        if (recipe.Nutrition is { } n && n.HasData)
            meta.Add($"{n.Calories:0} kcal");
        meta.Add($"{recipe.Ingredients.Count} ingredients");

        stack.Add(new Label
        {
            Text = string.Join("  ·  ", meta),
            FontSize = 12,
            TextColor = Color.FromArgb("#8a8078")
        });

        return new Border
        {
            BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Stroke = Color.FromArgb("#e0d9ce"),
            StrokeThickness = 1,
            Padding = new Thickness(20, 16),
            Content = stack
        };
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(Home));
    }
}
