namespace Voba.Pages;

public partial class Home : ContentPage
{
    public Home()
    {
        InitializeComponent();
    }

    private async void OnCreateNewTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("Forum");
    }

    private async void OnSavedTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("SavedRecipes");
    }
}