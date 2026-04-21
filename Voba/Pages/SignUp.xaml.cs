using Voba.Models;
using Voba.Services;
using Voba.Repositories;

namespace Voba.Pages;

public partial class SignUp : ContentPage
{
    public SignUp()
    {
        InitializeComponent();
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        string username = UsernameEntry.Text;
        string password = PasswordEntry.Text;
        string confirmPassword = ConfirmPasswordEntry.Text;
        if (password != confirmPassword)
        {
            await DisplayAlert("Error", "Passwords do not match. Please try again.", "OK");
            return;
        }

        var hasher = new BcryptPasswordHasher();
        var userRepo = new UserRepository();
        var authRepo = new AuthDataRepository();
        var JwtService = new JwtService();
        var hasher = new AuthService();
        var user = new AuthData(username, authRepo,, JwtService);
        user.SetPassword(password, hasher);
        bool isSignUpSuccessful = await CreateUser(username, password); // when method is implemented
        if (isSignUpSuccessful)
        {
            await Shell.Current.GoToAsync("//Home");
        }
        else
        {
            await DisplayAlert("Sign Up Failed", "An error occurred while creating your account. Please try again.", "OK");
        }
    }
}