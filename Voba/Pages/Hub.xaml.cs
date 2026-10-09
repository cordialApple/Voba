using Voba.Client;

namespace Voba.Pages;

public partial class Hub : ContentPage
{
    private readonly IVobaApiClient _api;

    public Hub(IVobaApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void OnNewRecipeClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(Forum));

    private async void OnSavedRecipesClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(SavedRecipes));

    private async void OnSignOutTapped(object sender, TappedEventArgs e)
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
