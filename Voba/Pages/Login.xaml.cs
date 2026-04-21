using Voba.Interfaces;

namespace Voba.Pages;

public partial class Login : ContentPage
{
    private readonly IAuthService _authService;

    public Login(IAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var email = UsernameEntry.Text?.Trim() ?? string.Empty;
        var password = PasswordEntry.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Login", "Please enter your username and password.", "OK");
            return;
        }

        try
        {
            var result = await _authService.LoginAsync(email, password);
            if (result.Success)
                await Shell.Current.GoToAsync($"//{nameof(Home)}");
            else
                await DisplayAlert("Login failed", result.ErrorMessage ?? "Incorrect username or password.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Login failed", ex.Message, "OK");
        }
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SignUp));
    }
}
