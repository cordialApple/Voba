using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Voba.AI.Pipeline.Handlers;
using Voba.Models;
using Voba.Services;

namespace Voba;

public partial class MainPage : ContentPage
{
    private readonly IAiChatService _aiService;
    private readonly GemmaIdeationHandler _ideationHandler;
    private readonly SpoonacularPricingHandler _pricingHandler;
    private readonly GemmaFullRecipeHandler _fullRecipeHandler;

    // Holds the context between the Generate step and the Select step.
    // Populated by OnGenerateClicked, read by OnSelectRecipeXClicked.
    private RecipeGenerationContext? _currentContext;

    // ── Constructor ──────────────────────────────────────────────────────────
    // Chain: GemmaIdeationHandler → SpoonacularPricingHandler → GemmaFullRecipeHandler
    //
    // NOTE: The full-recipe handler is NOT chained during Generate.
    // It is invoked separately when the user presses a "Cook This Recipe" button,
    // so Gemma only does the expensive full-recipe generation for the chosen option.
    public MainPage(
        IAiChatService aiService,
        GemmaIdeationHandler ideationHandler,
        SpoonacularPricingHandler pricingHandler,
        GemmaFullRecipeHandler fullRecipeHandler)
    {
        InitializeComponent();
        _aiService = aiService;
        _ideationHandler = ideationHandler;
        _pricingHandler = pricingHandler;
        _fullRecipeHandler = fullRecipeHandler;

        // Ideation → Pricing only. Full recipe is triggered on demand.
        _ideationHandler.SetNext(_pricingHandler);
    }

    // ── STEP 1: Generate recipe ideas ────────────────────────────────────────

    private async void OnGenerateClicked(object sender, EventArgs e)
    {
        // Reset everything from a previous run.
        ResetRecipeCards();
        ResetInstructionsPanel();
        _currentContext = null;

        TestButton.IsEnabled = false;
        LoadingSpinner.IsRunning = true;
        LoadingSpinner.IsVisible = true;
        PromptDebugHeader.IsVisible = false;
        PromptDebugBorder.IsVisible = false;
        PromptDebugLabel.Text = string.Empty;

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
                { ChkKeto,        "Keto"        },
                { ChkPaleo,       "Paleo"       },
                { ChkGlutenFree,  "Gluten-Free" },
                { ChkDairyFree,   "Dairy-Free"  },
                { ChkHalal,       "Halal"       },
                { ChkKosher,      "Kosher"      },
            };

            foreach (var (checkbox, dietName) in dietMap)
                if (checkbox.IsChecked) restrictions.Add(dietName);

            if (!string.IsNullOrWhiteSpace(AllergyInput.Text))
            {
                restrictions.AddRange(
                    AllergyInput.Text
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => a.Trim())
                        .Where(a => !string.IsNullOrWhiteSpace(a)));
            }

            string? cuisinePreference = string.IsNullOrWhiteSpace(CuisineInput.Text)
                ? null
                : CuisineInput.Text.Trim();

            // ── Run ideation + pricing pipeline ──────────────────────────
            _currentContext = new RecipeGenerationContext
            {
                ServingSize = servings,
                TargetBudget = budget,
                DietaryRestrictions = restrictions,
                CuisinePreference = cuisinePreference
            };

            await _ideationHandler.HandleAsync(_currentContext);

            // ── Debug prompt ──────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(_currentContext.DebugPrompt))
            {
                PromptDebugLabel.Text = _currentContext.DebugPrompt;
                PromptDebugHeader.IsVisible = true;
                PromptDebugBorder.IsVisible = true;
            }

            // ── Populate recipe cards ─────────────────────────────────────
            var options = _currentContext.ProposedOptions;

            if (options.Count == 0)
            {
                await DisplayAlert("No Results", "Gemma returned no recipe ideas. Try adjusting your budget or restrictions.", "OK");
                return;
            }

            RecipeCardsPanel.IsVisible = true;

            if (options.Count >= 1) PopulateCard(1, options[0]);
            if (options.Count >= 2) PopulateCard(2, options[1]);
        }
        catch (JsonException jsonEx)
        {
            await DisplayAlert("Parse Error", $"JSON PARSE FAILED:\n{jsonEx.Message}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            LoadingSpinner.IsRunning = false;
            LoadingSpinner.IsVisible = false;
            TestButton.IsEnabled = true;
        }
    }

    // ── STEP 2a: User selects recipe 1 ───────────────────────────────────────

    private async void OnSelectRecipe1Clicked(object sender, EventArgs e)
    {
        if (_currentContext?.ProposedOptions.Count >= 1)
            await GenerateInstructions(_currentContext.ProposedOptions[0]);
    }

    // ── STEP 2b: User selects recipe 2 ───────────────────────────────────────

    private async void OnSelectRecipe2Clicked(object sender, EventArgs e)
    {
        if (_currentContext?.ProposedOptions.Count >= 2)
            await GenerateInstructions(_currentContext.ProposedOptions[1]);
    }

    // ── Full recipe generation ───────────────────────────────────────────────

    private async Task GenerateInstructions(RecipeOption selectedOption)
    {
        if (_currentContext == null) return;

        // Set the selected option on the shared context so GemmaFullRecipeHandler
        // can read it — this is the field it checks before building the prompt.
        _currentContext.SelectedOption = selectedOption;
        _currentContext.IsHandled = false;
        _currentContext.FinalRecipe = null;

        // Show the instructions panel in loading state.
        InstructionsPanel.IsVisible = true;
        InstructionsSpinner.IsRunning = true;
        InstructionsSpinner.IsVisible = true;
        InstructionsTitle.Text = $"Generating instructions for {selectedOption.Name}...";
        InstructionsTitle.TextColor = Colors.Gray;
        InstructionsLabel.Text = string.Empty;

        // Disable both select buttons while generating.
        SelectBtn1.IsEnabled = false;
        SelectBtn2.IsEnabled = false;

        try
        {
            // Run ONLY the full recipe handler — ideation and pricing already ran.
            await _fullRecipeHandler.HandleAsync(_currentContext);

            if (_currentContext.FinalRecipe != null)
            {
                InstructionsTitle.Text = _currentContext.FinalRecipe.Title;
                InstructionsTitle.TextColor = Colors.Black;
                InstructionsLabel.Text = _currentContext.FinalRecipe.Instructions;
            }
            else
            {
                InstructionsTitle.Text = "Could not generate instructions.";
                InstructionsTitle.TextColor = Colors.Red;
            }
        }
        catch (Exception ex)
        {
            InstructionsTitle.Text = "Error generating instructions.";
            InstructionsTitle.TextColor = Colors.Red;
            InstructionsLabel.Text = ex.Message;
        }
        finally
        {
            InstructionsSpinner.IsRunning = false;
            InstructionsSpinner.IsVisible = false;
            SelectBtn1.IsEnabled = true;
            SelectBtn2.IsEnabled = true;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void PopulateCard(int cardNumber, RecipeOption recipe)
    {
        if (cardNumber == 1)
        {
            Card1Title.Text = recipe.Name;
            Card1Ingredients.Text = $"🛒 {string.Join(", ", recipe.Ingredients)}";
            Card1Cost.Text = $"💰 Per serving: ${recipe.EstimatedCost:F2}";
            Card1TotalCost.Text = $"💰 Total ({_currentContext!.ServingSize} servings): ${recipe.TotalCost:F2}";
            Card1.IsVisible = true;
        }
        else if (cardNumber == 2)
        {
            Card2Title.Text = recipe.Name;
            Card2Ingredients.Text = $"🛒 {string.Join(", ", recipe.Ingredients)}";
            Card2Cost.Text = $"💰 Per serving: ${recipe.EstimatedCost:F2}";
            Card2TotalCost.Text = $"💰 Total ({_currentContext!.ServingSize} servings): ${recipe.TotalCost:F2}";
            Card2.IsVisible = true;
        }
    }

    private void ResetRecipeCards()
    {
        RecipeCardsPanel.IsVisible = false;
        Card1.IsVisible = false;
        Card2.IsVisible = false;
        Card1Title.Text = string.Empty;
        Card1Ingredients.Text = string.Empty;
        Card1Cost.Text = string.Empty;
        Card1TotalCost.Text = string.Empty;
        Card2Title.Text = string.Empty;
        Card2Ingredients.Text = string.Empty;
        Card2Cost.Text = string.Empty;
        Card2TotalCost.Text = string.Empty;
    }

    private void ResetInstructionsPanel()
    {
        InstructionsPanel.IsVisible = false;
        InstructionsTitle.Text = string.Empty;
        InstructionsLabel.Text = string.Empty;
    }
}