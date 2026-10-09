using Voba.AI.Pipeline.Handlers;
using Voba.Models;
using Voba.Services;

namespace Voba.Pages;

public partial class Forum : ContentPage
{
    private readonly GemmaIdeationHandler _ideationHandler;
    private readonly RecipeGenerationCoordinator _coordinator;

    public Forum(GemmaIdeationHandler ideationHandler, SpoonacularPricingHandler pricingHandler,
        RecipeGenerationCoordinator coordinator)
    {
        InitializeComponent();

        // Assemble the chain: ideation produces recipes, then enrichment adds
        // Spoonacular cost + nutrition before the user picks one.
        ideationHandler.SetNext(pricingHandler);
        _ideationHandler = ideationHandler;
        _coordinator = coordinator;
    }

    private async void OnGenerateClicked(object sender, EventArgs e)
    {
        if (!RecipeGenerationPolicy.TryParseInputs(BudgetInput.Text, ServingsInput.Text,
                out var budget, out var servings))
        {
            ErrorLabel.Text = "Enter a positive budget and serving size.";
            ErrorLabel.IsVisible = true;
            return;
        }

        ErrorLabel.IsVisible = false;
        GenerateButton.IsEnabled = false;
        Spinner.IsRunning = true;
        Spinner.IsVisible = true;

        try
        {
            // Restrictions
            var restrictions = new List<string>();

            var dietMap = new Dictionary<CheckBox, string>
            {
                { ChkVegan,       "Vegan"       },
                { ChkVegetarian,  "Vegetarian"  },
                { ChkKeto,        "Keto"        },
                { ChkPaleo,       "Paleo"       },
                { ChkGlutenFree,  "Gluten-Free" },
                { ChkDairyFree,   "Dairy-Free"  },
                { ChkHalal,       "Halal"       },
                { ChkKosher,      "Kosher"      },
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
            // Pushes to recipe generation
            var context = new RecipeGenerationContext
            {
                ServingSize = servings,
                TargetBudget = budget,
                DietaryRestrictions = restrictions,
                CuisinePreference = string.IsNullOrWhiteSpace(CuisineInput.Text)
                                           ? null
                                           : CuisineInput.Text.Trim()
            };

            var minimumSource = AppConfiguration.UseFakeEnrichment
                ? RecipeDataSource.Synthetic : RecipeDataSource.Real;
            context = await _coordinator.GetOptionsAsync(context, minimumSource, async generated =>
            {
                await _ideationHandler.HandleAsync(generated);
                generated.ProposedOptions = RecipeGenerationPolicy.WithinBudget(
                    generated.ProposedOptions, generated.TargetBudget, generated.ServingSize);
            });

            if (context.ProposedOptions.Count == 0)
            {
                ErrorLabel.Text = "No recipes returned. Try adjusting your budget or restrictions.";
                ErrorLabel.IsVisible = true;
                return;
            }

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
        await Shell.Current.GoToAsync(nameof(Home));
    }
}
