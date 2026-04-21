using Voba.Interfaces;

namespace Voba.Pages;

public partial class SignUp : ContentPage
{
    private readonly IAuthService _authService;

    public SignUp(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;
        var confirmPassword = ConfirmPasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Sign up", "Please fill in all fields.", "OK");
            return;
        }

        if (password != confirmPassword)
        {
            await DisplayAlert("Sign up", "Passwords do not match.", "OK");
            return;
        }

        try
        {
            var result = await _authService.RegisterAsync(username, username, password);
            if (result.Success)
                await Shell.Current.GoToAsync($"//{nameof(Home)}");
            else
                await DisplayAlert("Sign up failed", result.ErrorMessage ?? "Unable to create account.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Sign up failed", ex.Message, "OK");
        }
    }
}
