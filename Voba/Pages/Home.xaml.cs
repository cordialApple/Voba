namespace Voba.Pages;

public partial class Home : ContentPage
{
    public Home()
    {
        InitializeComponent();
    }

    private async void OnCreateNewTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(Forum));
    }

    private async void OnSavedTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(SavedRecipes));
    }
}