namespace Voba.Pages;

public partial class Hub : ContentPage
{
    public Hub()
    {
        InitializeComponent();
    }

    private async void OnNewRecipeClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(Forum));
    }

    private async void OnSavedRecipesClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SavedRecipes));
    }

    private async void OnSignOutTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync($"//{nameof(Home)}");
    }
}