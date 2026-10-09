using Voba.Services;

namespace Voba.Pages;

public partial class Home : ContentPage
{
    private readonly ICurrentUserService _currentUser;

    public Home(ICurrentUserService currentUser)
    {
        InitializeComponent();
        _currentUser = currentUser;
    }

    private async void OnCreateNewTapped(object sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(nameof(Forum));

    private async void OnSavedTapped(object sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync(nameof(SavedRecipes));

    private async void OnSignOutClicked(object sender, EventArgs e)
    {
        _currentUser.Clear();
        await Shell.Current.GoToAsync("//Login");
    }
}
