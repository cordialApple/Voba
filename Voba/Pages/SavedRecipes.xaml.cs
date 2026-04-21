namespace Voba.Pages;

public partial class SavedRecipes : ContentPage
{
    public SavedRecipes()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"//{nameof(Hub)}");
    }
}