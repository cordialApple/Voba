namespace Voba
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("Login", typeof(ContentPage));
            Routing.RegisterRoute("SavedRecipes", typeof(ContentPage));
            Routing.RegisterRoute("Forum", typeof(ContentPage));
            Routing.RegisterRoute("Recipe", typeof(ContentPage));
            Routing.RegisterRoute("Home", typeof(ContentPage));
            Routing.RegisterRoute("RecipeSelect", typeof(ContentPage));
            Routing.RegisterRoute("SignUp", typeof(ContentPage));
        }
    }
}
