using Voba.Client;

namespace Voba.Pages;

public partial class Home : ContentPage
{
    private readonly IVobaApiClient _api;

    public Home(IVobaApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void OnCreateNewTapped(object sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(nameof(Forum));

    private async void OnSavedTapped(object sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(nameof(SavedRecipes));

    private async void OnSignOutClicked(object sender, EventArgs e)
    {
        try
        {
            await _api.LogoutAsync();
        }
        catch (Exception)
        {
            await DisplayAlert("Sign out", "Signed out locally. Server session could not be revoked.", "OK");
        }
        await Shell.Current.GoToAsync("//Login");
    }
}
