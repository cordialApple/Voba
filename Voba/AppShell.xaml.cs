namespace Voba
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("Login", typeof(Login));
            Routing.RegisterRoute("SavedRecipes", typeof(SavedRecipes));
            Routing.RegisterRoute("Forum", typeof(Forum));
            Routing.RegisterRoute("Recipe", typeof(Recipe));
            Routing.RegisterRoute("Home", typeof(Home));
            Routing.RegisterRoute("RecipeSelect", typeof(RecipeSelect));
        }
    }
}
