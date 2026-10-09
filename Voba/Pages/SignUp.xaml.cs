using Voba.Client;

namespace Voba.Pages;

public partial class SignUp : ContentPage
{
    private readonly IVobaApiClient _api;

    public SignUp(IVobaApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnLoginTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
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
        SignUpButton.IsEnabled = false;
        try
        {
            await _api.RegisterAsync(EmailEntry.Text.Trim(), NameEntry.Text.Trim(),
                PasswordEntry.Text);
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
            SignUpButton.IsEnabled = true;
        }
    }
}
