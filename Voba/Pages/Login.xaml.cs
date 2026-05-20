using Voba.Interfaces;
using Voba.Services;

namespace Voba.Pages;

public partial class Login : ContentPage
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public Login(IAuthService authService, ICurrentUserService currentUser)
    {
        InitializeComponent();
        _authService = authService;
        _currentUser = currentUser;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(EmailEntry.Text) ||
            string.IsNullOrWhiteSpace(PasswordEntry.Text))
        {
            ErrorLabel.Text = "Please enter your email and password.";
            ErrorLabel.IsVisible = true;
            return;
        }
        ErrorLabel.IsVisible = false;

        var result = await _authService.LoginAsync(EmailEntry.Text.Trim(), PasswordEntry.Text);

        if (!result.Success || result.Data is null)
        {
            ErrorLabel.Text = result.ErrorMessage ?? "Login failed.";
            ErrorLabel.IsVisible = true;
            return;
        }

        _currentUser.SetUser(result.Data.UserId, EmailEntry.Text.Trim());
        await Shell.Current.GoToAsync(nameof(Home));
    }

    private async void OnSignUpTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SignUp));
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SignUp));
    }
}