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

    // Chain: GemmaIdeationHandler → SpoonacularPricingHandler → GemmaFullRecipeHandler
    public MainPage(
        IAiChatService aiService,
        GemmaIdeationHandler ideationHandler,
        SpoonacularPricingHandler pricingHandler,
        GemmaFullRecipeHandler fullRecipeHandler)
    {
        InitializeComponent();
        _aiService = aiService;
        _ideationHandler = ideationHandler;

        _ideationHandler
            .SetNext(pricingHandler)
            .SetNext(fullRecipeHandler);
    }

    private async void OnTestButtonClicked(object sender, EventArgs e)
    {
        TestButton.IsEnabled = false;
        LoadingSpinner.IsRunning = true;
        LoadingSpinner.IsVisible = true;

        PromptDebugHeader.IsVisible = false;
        PromptDebugBorder.IsVisible = false;
        PromptDebugLabel.Text = string.Empty;

        ResponseHeader.IsVisible = false;
        ResponseLabel.Text = "Brainstorming recipe concepts...";
        ResponseLabel.TextColor = Colors.Black;

        try
        {
            decimal budget = decimal.TryParse(BudgetInput.Text, out var b) ? b : 15.00m;
            int servings = int.TryParse(ServingsInput.Text, out var s) ? s : 2;

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
            {
                if (checkbox.IsChecked)
                    restrictions.Add(dietName);
            }

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

            var context = new RecipeGenerationContext
            {
                ServingSize = servings,
                TargetBudget = budget,
                DietaryRestrictions = restrictions,
                CuisinePreference = cuisinePreference
            };

            await _ideationHandler.HandleAsync(context);

            if (!string.IsNullOrWhiteSpace(context.DebugPrompt))
            {
                PromptDebugLabel.Text = context.DebugPrompt;
                PromptDebugHeader.IsVisible = true;
                PromptDebugBorder.IsVisible = true;
            }

            ResponseHeader.IsVisible = true;

            if (context.ProposedOptions.Count > 0)
            {
                string cuisineTag = cuisinePreference != null ? $" ({cuisinePreference})" : string.Empty;
                string outputText = $"SUCCESS! Found {context.ProposedOptions.Count} idea(s){cuisineTag}:\n\n";

                foreach (var idea in context.ProposedOptions)
                {
                    outputText += $"🍽️  {idea.Name}\n";
                    outputText += $"🛒  {string.Join(", ", idea.Ingredients)}\n";
                    outputText += $"💰  Est. ${idea.EstimatedCost:F2}\n\n";
                }

                ResponseLabel.Text = outputText;
                ResponseLabel.TextColor = Colors.Green;
            }
            else
            {
                ResponseLabel.Text = "No concepts returned. Gemma may have output malformed JSON.";
                ResponseLabel.TextColor = Colors.Orange;
            }
        }
        catch (JsonException jsonEx)
        {
            ResponseHeader.IsVisible = true;
            ResponseLabel.Text = $"JSON PARSE FAILED:\n\n{jsonEx.Message}";
            ResponseLabel.TextColor = Colors.Red;
        }
        catch (Exception ex)
        {
            ResponseHeader.IsVisible = true;
            ResponseLabel.Text = $"ERROR:\n\n{ex.Message}";
            ResponseLabel.TextColor = Colors.Red;
        }
        finally
        {
            LoadingSpinner.IsRunning = false;
            LoadingSpinner.IsVisible = false;
            TestButton.IsEnabled = true;
        }
    }
}