using Voba.Client;

namespace Voba.Pages;

public partial class Login : ContentPage
{
    private readonly IVobaApiClient _api;

    public Login(IVobaApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Login");
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
        LoginButton.IsEnabled = false;
        try
        {
            await _api.LoginAsync(EmailEntry.Text.Trim(), PasswordEntry.Text);
            await Shell.Current.GoToAsync(nameof(Home));
        }
        catch (VobaApiException ex)
        {
            ErrorLabel.Text = ex.Message;
            ErrorLabel.IsVisible = true;
        }
        catch (HttpRequestException)
        {
            ErrorLabel.Text = "Backend unavailable. Start the Voba backend and try again.";
            ErrorLabel.IsVisible = true;
        }
        catch (TaskCanceledException)
        {
            ErrorLabel.Text = "Backend timed out. Try again.";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
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
