namespace Voba.Pages;

public partial class RecipeSelect : ContentPage
{
    public string title1 { get; set; } = ;
    public string title2 { get; set; } = "Recipe 2";
    public string instructions1 { get; set; } = "Instructions for Recipe 1";
    public string instructions2 { get; set; } = "Instructions for Recipe 2";

    public RecipeSelect()
    {
        InitializeComponent();
    }

    private async void OnRecipeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0)
        {
            var selectedRecipe = e.CurrentSelection[0] as RecipeModel;
        }