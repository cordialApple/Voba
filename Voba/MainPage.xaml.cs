/*
namespace Voba
{
    public partial class MainPage : ContentPage
    {
        int count = 0;

        public MainPage()
        {
            InitializeComponent();
        }

        private void OnCounterClicked(object? sender, EventArgs e)
        {
            count++;

            if (count == 1)
                CounterBtn.Text = $"Clicked {count} time";
            else
                CounterBtn.Text = $"Clicked {count} times";

            SemanticScreenReader.Announce(CounterBtn.Text);
        }
    }
}
*/

/*
using System;
using Microsoft.Maui.Controls;
using Voba.Services;

namespace Voba;

public partial class MainPage : ContentPage
{
    private readonly IAiChatService _aiService;

    // MAUI's Dependency Injection automatically provides your Semantic Kernel service here
    public MainPage(IAiChatService aiService)
    {
        InitializeComponent();
        _aiService = aiService;
    }

    private async void OnTestButtonClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PromptInput.Text)) return;

        // 1. Update UI to show loading state   
        TestButton.IsEnabled = false;
        LoadingSpinner.IsRunning = true;
        LoadingSpinner.IsVisible = true;
        ResponseLabel.Text = "Sending to localhost:11434...";
        ResponseLabel.TextColor = Colors.Black;

        try
        {
            // 2. Call your local Gemma 3 model
            string response = await _aiService.GetResponseAsync(PromptInput.Text);

            // 3. Display success
            ResponseLabel.Text = response;
            ResponseLabel.TextColor = Colors.Green;
        }
        catch (Exception ex)
        {
            // 4. Catch the exact error if it fails (e.g. Windows Sandbox blocking localhost)
            ResponseLabel.Text = $"CONNECTION FAILED:\n\n{ex.Message}\n\nDid you run the CheckNetIsolation LoopbackExempt command in PowerShell?";
            ResponseLabel.TextColor = Colors.Red;
        }
        finally
        {
            // 5. Reset UI
            LoadingSpinner.IsRunning = false;
            LoadingSpinner.IsVisible = false;
            TestButton.IsEnabled = true;
        }
    }
}
*/

using Azure.Core;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Text.Json;
using Voba.AI.Pipeline.Handlers;
using Voba.Models;
using Voba.Services;

namespace Voba;

public partial class MainPage : ContentPage
{
    private readonly IAiChatService _aiService;
    private readonly GemmaIdeationHandler _ideationHandler;

    public MainPage(IAiChatService aiService, GemmaIdeationHandler ideationHandler, GemmaFullRecipeHandler fullRecipeHandler)
    {
        InitializeComponent();
        _aiService = aiService;
        _ideationHandler = ideationHandler;
        _ideationHandler.SetNext(fullRecipeHandler);
    }

    private async void OnTestButtonClicked(object sender, EventArgs e)
    {
        // ── Reset UI ──────────────────────────────────────────────────────
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

            // Replace the old single-field block in OnTestButtonClicked:

            var restrictions = new List<string>();

            // Lifestyle diets — collect all checked boxes
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

            // Allergies free-text (unchanged)
            if (!string.IsNullOrWhiteSpace(AllergyInput.Text))
            {
                var allergyEntries = AllergyInput.Text
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrWhiteSpace(a));

                restrictions.AddRange(allergyEntries);
            }

            // 2. Allergies typed free-form, comma-separated (e.g. "nuts, soy")
            if (!string.IsNullOrWhiteSpace(AllergyInput.Text))
            {
                var allergyEntries = AllergyInput.Text
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrWhiteSpace(a));

                restrictions.AddRange(allergyEntries);
            }

            var context = new RecipeGenerationContext
            {
                ServingSize = servings,
                TargetBudget = budget,
                DietaryRestrictions = restrictions
            };

            // ── Run pipeline ──────────────────────────────────────────────
            await _ideationHandler.HandleAsync(context);

            // ── Show prompt that was sent to Gemma ────────────────────────
            if (!string.IsNullOrWhiteSpace(context.DebugPrompt))
            {
                PromptDebugLabel.Text = context.DebugPrompt;
                PromptDebugHeader.IsVisible = true;
                PromptDebugBorder.IsVisible = true;
            }

            // ── Show model response ───────────────────────────────────────
            ResponseHeader.IsVisible = true;

            if (context.ProposedOptions.Count > 0)
            {
                string outputText = $"SUCCESS! Found {context.ProposedOptions.Count} ideas:\n\n";
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
