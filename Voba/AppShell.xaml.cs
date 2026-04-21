using Voba.Pages;

namespace Voba
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(Home), typeof(Home));
            Routing.RegisterRoute(nameof(Login), typeof(Login));
            Routing.RegisterRoute(nameof(SignUp), typeof(SignUp));
            Routing.RegisterRoute(nameof(Hub), typeof(Hub));
            Routing.RegisterRoute(nameof(Forum), typeof(Forum));
            Routing.RegisterRoute(nameof(RecipeSelect), typeof(RecipeSelect));
            Routing.RegisterRoute(nameof(Recipe), typeof(Recipe));
            Routing.RegisterRoute(nameof(SavedRecipes), typeof(SavedRecipes));
        }
    }
}
