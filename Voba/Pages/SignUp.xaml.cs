using Voba.Interfaces;
using Voba.Services;

namespace Voba.Pages;

public partial class SignUp : ContentPage
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public SignUp(IAuthService authService, ICurrentUserService currentUser)
    {
        InitializeComponent();
        _authService = authService;
        _currentUser = currentUser;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnLoginTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(Login));
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameEntry.Text) ||
            string.IsNullOrWhiteSpace(EmailEntry.Text) ||
            string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            ErrorLabel.Text = "Please fill in all fields.";
            ErrorLabel.IsVisible = true;
            return;
        }
        ErrorLabel.IsVisible = false;

        var result = await _authService.RegisterAsync(
            EmailEntry.Text.Trim(), NameEntry.Text.Trim(), PasswordEntry.Text);

        if (!result.Success || result.Data is null)
        {
            ErrorLabel.Text = result.ErrorMessage ?? "Sign-up failed.";
            ErrorLabel.IsVisible = true;
            return;
        }

        _currentUser.SetUser(result.Data.Id, EmailEntry.Text.Trim());
        await Shell.Current.GoToAsync(nameof(Home));
    }
}