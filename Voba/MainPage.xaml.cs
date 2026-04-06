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
