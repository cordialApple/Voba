using Voba.Models;

namespace Voba.Pages;

public partial class RecipeSelect : ContentPage
{
    public string title1 { get; set; }
    public string title2 { get; set; }
    public List<string> ingredients1 { get; set; }
    public List<string> ingredients2 { get; set; }
    public RecipeSelect()
    {
        InitializeComponent();
        BindingContext = this;
        var context = new RecipeOption();
        title1 = context.Name;
        ingredients1 = context.Ingredients;

    }

    private async void OnRecipeSelected(object sender, SelectionChangedEventArgs e)
    {

        await Shell.Current.GoToAsync("Recipe");
    }
}