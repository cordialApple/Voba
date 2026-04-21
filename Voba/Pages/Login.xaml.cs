using Voba.Models;

namespace Voba.Pages;

public partial class Login : ContentPage
{
    public Login()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        string username = UsernameEntry.Text;
        string password = PasswordEntry.Text;
        var user = new AuthData(username);
        bool isLoginCorrect = user.VerifyPassword(password,hasher); //await passwordCheck(username, password); // when method is implemented
        if (isLoginCorrect)
        {
            await Shell.Current.GoToAsync("//Home");
        }
        else
        {
            await DisplayAlert("Login Failed", "Incorrect username or password. Please try again.", "OK");
        }
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("SignUp");
    }
}