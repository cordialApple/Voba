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

using System;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Voba.Services;
using Voba.Models;
using Voba.AI.Pipeline.Handlers;

namespace Voba;

public partial class MainPage : ContentPage
{
    private readonly IAiChatService _aiService;
    private readonly GemmaIdeationHandler _ideationHandler;

    public MainPage(IAiChatService aiService, GemmaIdeationHandler ideationHandler)
    {
        InitializeComponent();
        _aiService = aiService;
        _ideationHandler = ideationHandler;
    }

    private async void OnTestButtonClicked(object sender, EventArgs e)
    {
        TestButton.IsEnabled = false;
        LoadingSpinner.IsRunning = true;
        LoadingSpinner.IsVisible = true;
        ResponseLabel.Text = "Brainstorming recipe concepts...";
        ResponseLabel.TextColor = Colors.Black;

        try
        {
            decimal budget = decimal.TryParse(BudgetInput.Text, out var parsedBudget) ? parsedBudget : 15.00m;
            int servings = int.TryParse(ServingsInput.Text, out var parsedServings) ? parsedServings : 2;

            var restrictions = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrWhiteSpace(DietInput.Text))
            {
                restrictions.Add(DietInput.Text.Trim());
            }

            var context = new RecipeGenerationContext
            {
                ServingSize = servings,
                TargetBudget = budget,
                DietaryRestrictions = restrictions
            };

            await _ideationHandler.HandleAsync(context);

            if (context.ProposedOptions.Count > 0)
            {
                string outputText = $"SUCCESS! Found {context.ProposedOptions.Count} ideas:\n\n";
                foreach (var idea in context.ProposedOptions)
                {
                    outputText += $"🍽️ {idea.Name}\n";
                    outputText += $"🛒 Ingredients: {string.Join(", ", idea.Ingredients)}\n\n";
                }
                ResponseLabel.Text = outputText;
                ResponseLabel.TextColor = Colors.Green;
            }
            else
            {
                ResponseLabel.Text = "No concepts returned. Gemma might have output malformed JSON.";
                ResponseLabel.TextColor = Colors.Orange;
            }
        }
        catch (JsonException jsonEx)
        {
            ResponseLabel.Text = $"JSON PARSE FAILED:\n\n{jsonEx.Message}";
            ResponseLabel.TextColor = Colors.Red;
        }
        catch (Exception ex)
        {
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
