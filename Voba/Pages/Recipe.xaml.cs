using Microsoft.Maui.Controls.Shapes;
using Voba.Models;

namespace Voba.Pages;

public partial class Recipe : ContentPage
{
    private readonly RecipeGenerationContext _context;

    // ── Constructor used by navigation (pass context as query parameter) ──────
    public Recipe(RecipeGenerationContext context)
    {
        InitializeComponent();
        _context = context;
        PopulatePage();
    }

    // ── Parameterless ctor keeps the XAML Hot Reload previewer happy ──────────
    public Recipe()
    {
        InitializeComponent();
        _context = BuildDesignTimeContext();
        PopulatePage();
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  PAGE POPULATION
    // ═════════════════════════════════════════════════════════════════════════

    private void PopulatePage()
    {
        var recipe = _context.FinalRecipe;
        var option = _context.SelectedOption;

        // Title
        RecipeTitleLabel.Text =
            recipe?.Title is { Length: > 0 } t ? t :
            option?.Name is { Length: > 0 } n ? n :
            "Your Recipe";

        // Stats
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

    // ═════════════════════════════════════════════════════════════════════════
    //  DIETARY / CUISINE CHIPS
    // ═════════════════════════════════════════════════════════════════════════

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
        Margin = new Thickness(0, 0, 6, 6),   // replaces FlexLayout Gap
        Content = new Label
        {
            Text = text,
            TextColor = Color.FromArgb(fg),
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            CharacterSpacing = 0.4
        }
    };

    // ═════════════════════════════════════════════════════════════════════════
    //  INGREDIENTS LIST
    // ═════════════════════════════════════════════════════════════════════════

    private void BuildIngredientsList(RecipeOption? option)
    {
        if (option?.Ingredients is not { Count: > 0 })
        {
            IngredientsLayout.Add(new Label
            {
                Text = "No ingredients listed.",
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
                wrapper.Add(new BoxView
                {
                    Color = Color.FromArgb("#4a6e48"),
                    HeightRequest = 1
                });
                IngredientsLayout.Add(wrapper);
            }
            else
            {
                IngredientsLayout.Add(row);
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  INSTRUCTIONS
    //
    //  GemmaFullRecipeHandler instructs Gemma to return ONLY lines in the
    //  format "N. step text" — no title, no preamble, no markdown, no tips.
    //  ParseNumberedSteps matches exactly that contract.
    //
    //  PlainInstructionsCard is kept in the XAML purely as an error state
    //  for the rare case where the local Gemma model returns garbled output.
    // ═════════════════════════════════════════════════════════════════════════

    private void BuildInstructions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            ShowErrorCard("Instructions could not be generated. Please try again.");
            return;
        }

        var steps = ParseNumberedSteps(raw);

        if (steps.Count == 0)
        {
            // Gemma returned something, but it didn't match the expected format.
            // Surface the raw text so the user isn't left with a blank screen.
            ShowErrorCard(raw.Trim());
            return;
        }

        StepCountLabel.Text = $"{steps.Count} steps";
        StepCountBadge.IsVisible = true;

        foreach (var (number, text) in steps)
            StepsLayout.Add(BuildStepCard(number, text));
    }

    /// <summary>
    /// Parses Gemma's guaranteed output format: "N. step text"
    /// One step per line, number followed by a period.
    /// </summary>
    private static List<(int Number, string Text)> ParseNumberedSteps(string raw)
    {
        var result = new List<(int, string)>();

        foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();

            // Matches exactly "1. Do something" — the only format Gemma produces
            // given the prompt in GemmaFullRecipeHandler ("Start directly with step 1").
            var match = System.Text.RegularExpressions.Regex.Match(
                trimmed, @"^(\d+)\.\s+(.+)$");

            if (match.Success
                && int.TryParse(match.Groups[1].Value, out int num)
                && match.Groups[2].Value is { Length: > 0 } stepText)
            {
                result.Add((num, stepText));
            }
        }

        return result;
    }

    /// <summary>
    /// Shown only when Gemma returns empty content or unparseable output.
    /// Not part of the normal happy path.
    /// </summary>
    private void ShowErrorCard(string message)
    {
        InstructionsLabel.Text = message;
        PlainInstructionsCard.IsVisible = true;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  STEP CARD BUILDER
    // ═════════════════════════════════════════════════════════════════════════

    private static Border BuildStepCard(int number, string text)
    {
        // Outer card
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

        // Step number badge
        var badge = new Border
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
        };

        var stepText = new Label
        {
            Text = text,
            FontSize = 15,
            TextColor = Color.FromArgb("#3a3228"),
            LineHeight = 1.65,
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center
        };

        inner.Add(badge, column: 0, row: 0);
        inner.Add(stepText, column: 1, row: 0);
        card.Content = inner;

        return card;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  BUTTON HANDLERS
    // ═════════════════════════════════════════════════════════════════════════

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnStartCookingClicked(object sender, EventArgs e)
    {
        await AnimateButton(StartCookingButton);

        // TODO: navigate to a step-by-step cooking mode, e.g.:
        // await Shell.Current.GoToAsync(nameof(CookingMode),
        //     new Dictionary<string, object> { ["Context"] = _context });

        await DisplayAlert("Let's Cook!",
            $"Starting step-by-step mode for \"{RecipeTitleLabel.Text}\".", "OK");
    }

    private async void OnSaveRecipeClicked(object sender, EventArgs e)
    {
        await AnimateButton(SaveRecipeButton);

        // TODO: persist to local SQLite / favourites service
        await DisplayAlert("Saved!", "Recipe added to your saved collection.", "Great 👍");
    }

    private static async Task AnimateButton(Button btn)
    {
        btn.Opacity = 0.65;
        await Task.Delay(100);
        btn.Opacity = 1.0;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  DESIGN-TIME SAMPLE DATA
    // ═════════════════════════════════════════════════════════════════════════

    private static RecipeGenerationContext BuildDesignTimeContext() => new()
    {
        ServingSize = 4,
        TargetBudget = 25.00m,
        CuisinePreference = "Italian",
        DietaryRestrictions = ["Vegetarian", "Gluten-Free"],
        SelectedOption = new RecipeOption
        {
            Name = "Creamy Mushroom Risotto",
            EstimatedCost = 18.50m,
            TotalCost = 18.50m,
            Ingredients =
            [
                "300 g Arborio rice",
                "200 g chestnut mushrooms, sliced",
                "1 litre warm vegetable stock",
                "1 medium onion, finely diced",
                "2 cloves garlic, minced",
                "120 ml dry white wine",
                "60 g Parmesan, grated",
                "2 tbsp unsalted butter",
                "2 tbsp olive oil",
                "Salt &amp; black pepper to taste",
                "Fresh parsley to garnish"
            ]
        },
        FinalRecipe = new FullRecipe
        {
            Title = "Creamy Mushroom Risotto",
            // Matches the exact format GemmaFullRecipeHandler prompts for:
            // "N. step text" — one step per line, no preamble, no title.
            Instructions =
                "1. Heat the olive oil and butter in a large heavy-bottomed pan over medium heat.\n" +
                "2. Add the diced onion and cook for 5 minutes until softened and translucent.\n" +
                "3. Stir in the garlic and mushrooms and cook for a further 4 minutes until golden.\n" +
                "4. Add the Arborio rice and stir for 2 minutes to coat the grains in the fat.\n" +
                "5. Pour in the white wine and stir continuously until fully absorbed.\n" +
                "6. Add the warm stock one ladle at a time, stirring constantly and waiting until each addition is absorbed before adding the next.\n" +
                "7. Continue for 18 to 20 minutes until the rice is creamy and al dente.\n" +
                "8. Remove from heat, stir in the Parmesan, and season generously with salt and pepper.\n" +
                "9. Rest for 2 minutes then serve garnished with fresh parsley."
        }
    };
}