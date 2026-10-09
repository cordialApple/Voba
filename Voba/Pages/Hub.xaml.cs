using Voba.Services;

namespace Voba.Pages;

public partial class Hub : ContentPage
{
    private readonly ICurrentUserService _currentUser;

    public Hub(ICurrentUserService currentUser)
    {
        InitializeComponent();
        _currentUser = currentUser;
    }

    private async void OnNewRecipeClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(Forum));

    private async void OnSavedRecipesClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(SavedRecipes));

    private async void OnSignOutTapped(object sender, TappedEventArgs e)
    {
        _currentUser.Clear();
        await Shell.Current.GoToAsync("//Login");
    }
}
