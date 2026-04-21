using Voba.AI.Pipeline.Handlers;
using Voba.Models;

namespace Voba.Pages;

public partial class Forum : ContentPage
{
    private readonly GemmaIdeationHandler _ideationHandler;
    private readonly SpoonacularPricingHandler _pricingHandler;

    public Forum(GemmaIdeationHandler ideationHandler, SpoonacularPricingHandler pricingHandler)
    {
        InitializeComponent();
        _ideationHandler = ideationHandler;
        _pricingHandler = pricingHandler;

        // Chain: Ideation → Pricing only.
        // Full recipe is triggered later on RecipeSelect when the user picks a card.
        _ideationHandler.SetNext(_pricingHandler);
    }

    private async void OnGenerateClicked(object sender, EventArgs e)
    {
        // ── Validation ────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(BudgetInput.Text) ||
            string.IsNullOrWhiteSpace(ServingsInput.Text))
        {
            ErrorLabel.Text = "Please enter a budget and serving size.";
            ErrorLabel.IsVisible = true;
            return;
        }

        ErrorLabel.IsVisible = false;
        GenerateButton.IsEnabled = false;
        Spinner.IsRunning = true;
        Spinner.IsVisible = true;

        try
        {
            decimal budget = decimal.TryParse(BudgetInput.Text, out var b) ? b : 15.00m;
            int servings = int.TryParse(ServingsInput.Text, out var s) ? s : 2;

            // ── Restrictions ──────────────────────────────────────────────
            var restrictions = new List<string>();

            var dietMap = new Dictionary<CheckBox, string>
            {
                { ChkVegan,       "Vegan"       },
                { ChkVegetarian,  "Vegetarian"  },
                { ChkKeto,        "Keto"         },
                { ChkPaleo,       "Paleo"        },
                { ChkGlutenFree,  "Gluten-Free"  },
                { ChkDairyFree,   "Dairy-Free"   },
                { ChkHalal,       "Halal"        },
                { ChkKosher,      "Kosher"       },
            };

            foreach (var (checkbox, name) in dietMap)
                if (checkbox.IsChecked) restrictions.Add(name);

            if (!string.IsNullOrWhiteSpace(AllergyInput.Text))
            {
                restrictions.AddRange(
                    AllergyInput.Text
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.Trim())
                        .Where(a => !string.IsNullOrWhiteSpace(a)));
            }

            var context = new RecipeGenerationContext
            {
                ServingSize = servings,
                TargetBudget = budget,
                DietaryRestrictions = restrictions,
                CuisinePreference = string.IsNullOrWhiteSpace(CuisineInput.Text)
                                           ? null
                                           : CuisineInput.Text.Trim()
            };

            // ── Run pipeline ──────────────────────────────────────────────
            await _ideationHandler.HandleAsync(context);

            if (context.ProposedOptions.Count == 0)
            {
                ErrorLabel.Text = "No recipes returned. Try adjusting your budget or restrictions.";
                ErrorLabel.IsVisible = true;
                return;
            }

            // ── Navigate to RecipeSelect, passing context ─────────────────
            await Shell.Current.GoToAsync(nameof(RecipeSelect),
                new Dictionary<string, object> { ["Context"] = context });
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Something went wrong: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            Spinner.IsRunning = false;
            Spinner.IsVisible = false;
            GenerateButton.IsEnabled = true;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"//{nameof(Hub)}");
    }
}