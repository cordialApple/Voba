using Microsoft.Maui.Controls.Shapes;
using System.Net;
using Voba.Client;
using Voba.Contracts;

namespace Voba.Pages;

[QueryProperty(nameof(FullRecipe), "FullRecipe")]
[QueryProperty(nameof(SavedRecipe), "SavedRecipe")]
public partial class Recipe : ContentPage
{
    private readonly IVobaApiClient? _api;
    private FullRecipeResponse? _full;
    private SavedRecipeResponse? _saved;

    public FullRecipeResponse? FullRecipe
    {
        set
        {
            _full = value;
            _saved = null;
            PopulatePage();
        }
    }

    public SavedRecipeResponse? SavedRecipe
    {
        set
        {
            _saved = value;
            _full = null;
            PopulatePage();
        }
    }

    public Recipe(IVobaApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    public Recipe()
    {
        InitializeComponent();
    }

    private void PopulatePage()
    {
        var option = _full?.SelectedOption;
        RecipeTitleLabel.Text = _full?.Title ?? _saved?.Title ?? "Your Recipe";
        ServingsLabel.Text = _full?.Servings.ToString() ?? "—";
        var cost = option?.TotalCost ?? _saved?.TotalCost ?? 0m;
        CostLabel.Text = cost > 0 ? $"${cost:F2}" : "—";
        SourceLabel.Text = option is not null
            ? RecipePresentation.SourceLabel(option.CostSource, option.NutritionSource,
                option.Nutrition is not null)
            : _saved is not null
                ? RecipePresentation.SourceLabel(_saved.CostSource, _saved.NutritionSource,
                    _saved.Nutrition is not null)
                : string.Empty;
        BudgetLabel.Text = _full?.Budget > 0 ? $"${_full.Budget:F2}" : "—";
        SaveRecipeButton.IsVisible = _full is not null && _saved is null;

        BuildDietaryTags(_full?.CuisinePreference, _full?.DietaryRestrictions);
        BuildNutrition(option?.Nutrition ?? _saved?.Nutrition);
        BuildIngredientsList(option?.Ingredients ?? _saved?.Ingredients
            .Select(ingredient => ingredient.Amount > 0
                ? $"{ingredient.Amount:0.##} {ingredient.Unit} {ingredient.Name}".Trim()
                : ingredient.Name).ToArray());
        BuildInstructions(_full?.Instructions ?? _saved?.Instructions);
    }

    private void BuildNutrition(NutritionResponse? nutrition)
    {
        if (nutrition is null)
        {
            NutritionPanel.IsVisible = false;
            return;
        }

        CaloriesLabel.Text = $"{nutrition.Calories:0}";
        ProteinLabel.Text = $"{nutrition.ProteinGrams:0.#}g";
        FatLabel.Text = $"{nutrition.FatGrams:0.#}g";
        CarbsLabel.Text = $"{nutrition.CarbGrams:0.#}g";
        NutritionPanel.IsVisible = true;
    }

    private void BuildDietaryTags(string? cuisine, IReadOnlyList<string>? restrictions)
    {
        DietaryTagsLayout.Children.Clear();
        DietaryTagsLayout.IsVisible = !string.IsNullOrWhiteSpace(cuisine) || restrictions?.Count > 0;
        if (!DietaryTagsLayout.IsVisible)
            return;

        if (!string.IsNullOrWhiteSpace(cuisine))
            DietaryTagsLayout.Add(Chip(cuisine, "#d4e8d2", "#2e4d2c"));

        foreach (var r in restrictions ?? [])
            DietaryTagsLayout.Add(Chip(r, "#f5e6d0", "#5c3d1e"));
    }

    private static Border Chip(string text, string bg, string fg) => new()
    {
        BackgroundColor = Color.FromArgb(bg),
        StrokeShape = new RoundRectangle { CornerRadius = 20 },
        Stroke = Colors.Transparent,
        Padding = new Thickness(10, 4),
        Margin = new Thickness(0, 0, 6, 6),
        Content = new Label
        {
            Text = text,
            TextColor = Color.FromArgb(fg),
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 0.4
        }
    };

    private void BuildIngredientsList(IReadOnlyList<string>? ingredients)
    {
        IngredientsLayout.Children.Clear();
        if (ingredients is not { Count: > 0 })
        {
            return;
        }

        for (int i = 0; i < ingredients.Count; i++)
        {
            bool isLast = i == ingredients.Count - 1;

            var row = new Grid
            {
                Padding = new Thickness(16, 11),
                BackgroundColor = i % 2 == 0
                    ? Color.FromArgb("#3d5c3b")
                    : Color.FromArgb("#456644"),
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                }
            };

            row.Add(new Ellipse
            {
                Fill = new SolidColorBrush(Color.FromArgb("#8ba888")),
                WidthRequest = 6,
                HeightRequest = 6,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 10, 0)
            }, column: 0, row: 0);

            row.Add(new Label
            {
                Text = ingredients[i],
                TextColor = Color.FromArgb("#dff0de"),
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center
            }, column: 1, row: 0);

            if (!isLast)
            {
                var wrapper = new VerticalStackLayout();
                wrapper.Add(row);
                wrapper.Add(new BoxView { Color = Color.FromArgb("#4a6e48"), HeightRequest = 1 });
                IngredientsLayout.Add(wrapper);
            }
            else
            {
                IngredientsLayout.Add(row);
            }
        }
    }

    private void BuildInstructions(string? raw)
    {
        StepsLayout.Children.Clear();
        PlainInstructionsCard.IsVisible = false;
        StepCountBadge.IsVisible = false;
        if (string.IsNullOrWhiteSpace(raw))
            return;
        var steps = ParseNumberedSteps(raw);
        if (steps.Count == 0) { ShowErrorCard(raw.Trim()); return; }

        StepCountLabel.Text = $"{steps.Count} steps";
        StepCountBadge.IsVisible = true;

        foreach (var (number, text) in steps)
            StepsLayout.Add(BuildStepCard(number, text));
    }

    private static List<(int Number, string Text)> ParseNumberedSteps(string raw)
    {
        var result = new List<(int Number, string Text)>();

        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            int dotIndex = trimmed.IndexOf('.');

            if (dotIndex > 0 && dotIndex < trimmed.Length - 1)
            {
                string numberPart = trimmed.Substring(0, dotIndex);

                if (int.TryParse(numberPart, out int num) &&
                    char.IsWhiteSpace(trimmed[dotIndex + 1]))
                {
                    string stepText = trimmed.Substring(dotIndex + 1).TrimStart();
                    if (stepText.Length > 0)
                        result.Add((num, stepText));
                }
            }
        }

        return result;
    }

    private void ShowErrorCard(string message)
    {
        InstructionsLabel.Text = message;
        PlainInstructionsCard.IsVisible = true;
    }

    private static Border BuildStepCard(int number, string text)
    {
        var card = new Border
        {
            BackgroundColor = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            Stroke = Color.FromArgb("#e0d9ce"),
            StrokeThickness = 1,
            Padding = new Thickness(24, 20)
        };

        var inner = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 16
        };

        inner.Add(new Border
        {
            BackgroundColor = Color.FromArgb("#e6f0e5"),
            StrokeShape = new RoundRectangle { CornerRadius = 24 },
            Stroke = Colors.Transparent,
            WidthRequest = 40,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = number.ToString(),
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#3d5c3b"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }
        }, column: 0, row: 0);

        inner.Add(new Label
        {
            Text = text,
            FontSize = 15,
            TextColor = Color.FromArgb("#3a3228"),
            LineHeight = 1.65,
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center
        }, column: 1, row: 0);

        card.Content = inner;
        return card;
    }

    private async void OnBackClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(Home));

    private async void OnStartCookingClicked(object sender, EventArgs e)
    {
        await AnimateButton(StartCookingButton);
        await DisplayAlert("Let's Cook!", $"Starting step-by-step mode for \"{RecipeTitleLabel.Text}\".", "OK");
    }

    private async void OnSaveRecipeClicked(object sender, EventArgs e)
    {
        await AnimateButton(SaveRecipeButton);

        if (_api is null || _full is null)
        {
            await DisplayAlert("Unavailable", "Saving isn't available right now.", "OK");
            return;
        }

        SaveRecipeButton.IsEnabled = false;
        try
        {
            _saved = await _api.SaveAsync(_full.DraftId, _full.DraftVersion);
            _full = null;
            PopulatePage();
            await DisplayAlert("Saved!", "Recipe added to your saved collection.", "Great");
        }
        catch (VobaApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await DisplayAlert("Selection changed", "Select the recipe again before saving.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Save failed", ex.Message, "OK");
        }
        finally
        {
            SaveRecipeButton.IsEnabled = true;
        }
    }

    private static async Task AnimateButton(Button btn)
    {
        btn.Opacity = 0.65;
        await Task.Delay(100);
        btn.Opacity = 1.0;
    }

}
