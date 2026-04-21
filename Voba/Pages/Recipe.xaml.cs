using Microsoft.Maui.Controls.Shapes;
using Voba.Models;

namespace Voba.Pages;

[QueryProperty(nameof(Context), "Context")]
public partial class Recipe : ContentPage
{
    private RecipeGenerationContext _context;

    public RecipeGenerationContext Context
    {
        set
        {
            _context = value;
            PopulatePage();
        }
    }

    public Recipe()
    {
        InitializeComponent();
        _context = BuildDesignTimeContext();
        PopulatePage();
    }

    private void PopulatePage()
    {
        var recipe = _context.FinalRecipe;
        var option = _context.SelectedOption;

        RecipeTitleLabel.Text =
            recipe?.Title is { Length: > 0 } t ? t :
            option?.Name is { Length: > 0 } n ? n :
            "Your Recipe";

        ServingsLabel.Text = _context.ServingSize > 0
            ? _context.ServingSize.ToString() : "—";

        CostLabel.Text =
            option?.TotalCost > 0 ? $"${option.TotalCost:F2}" :
            option?.EstimatedCost > 0 ? $"${option.EstimatedCost:F2}" : "—";

        BudgetLabel.Text = _context.TargetBudget > 0
            ? $"${_context.TargetBudget:F2}" : "—";

        BuildDietaryTags();
        BuildIngredientsList(option);
        BuildInstructions(recipe?.Instructions);
    }

    private void BuildDietaryTags()
    {
        bool hasContent = !string.IsNullOrWhiteSpace(_context.CuisinePreference)
                          || _context.DietaryRestrictions?.Count > 0;
        if (!hasContent) return;

        DietaryTagsLayout.IsVisible = true;

        if (!string.IsNullOrWhiteSpace(_context.CuisinePreference))
            DietaryTagsLayout.Add(Chip(_context.CuisinePreference!, "#d4e8d2", "#2e4d2c"));

        foreach (var r in _context.DietaryRestrictions ?? [])
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

    private void BuildIngredientsList(RecipeOption? option)
    {
        if (option?.Ingredients is not { Count: > 0 })
        {
            IngredientsLayout.Add(new Label
            {
                Text = "",
                TextColor = Color.FromArgb("#7aaa78"),
                FontSize = 13,
                Padding = new Thickness(16, 12)
            });
            return;
        }

        for (int i = 0; i < option.Ingredients.Count; i++)
        {
            bool isLast = i == option.Ingredients.Count - 1;

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
                Text = option.Ingredients[i],
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
        if (string.IsNullOrWhiteSpace(raw)) { ShowErrorCard(""); return; }
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

                if (int.TryParse(numberPart, out int num))
                {

                    if (char.IsWhiteSpace(trimmed[dotIndex + 1]))
                    {
                        string stepText = trimmed.Substring(dotIndex + 1).TrimStart();

                        if (stepText.Length > 0)
                        {
                            result.Add((num, stepText));
                        }
                    }
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

    private async void OnBackClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync($"{nameof(Home)}");
    private async void OnStartCookingClicked(object sender, EventArgs e)
    {
        await AnimateButton(StartCookingButton);
        await DisplayAlert("Let's Cook!", $"Starting step-by-step mode for \"{RecipeTitleLabel.Text}\".", "OK");
    }
    private async void OnSaveRecipeClicked(object sender, EventArgs e)
    {
        await AnimateButton(SaveRecipeButton);
        await DisplayAlert("Saved!", "Recipe added to your saved collection.", "Great");
    }
    private static async Task AnimateButton(Button btn)
    {
        btn.Opacity = 0.65;
        await Task.Delay(100);
        btn.Opacity = 1.0;
    }

    private static RecipeGenerationContext BuildDesignTimeContext() => new()
    {
        ServingSize = 4,
        TargetBudget = 25.00m,
        CuisinePreference = "",
        DietaryRestrictions = [""],
        SelectedOption = new RecipeOption
        {
            Name = "",
            EstimatedCost = 4.63m,
            TotalCost = 18.50m,
            Ingredients = []
        },
        FinalRecipe = new FullRecipe
        {
            Title = "",
            Instructions =
                ""
        }
    };
}