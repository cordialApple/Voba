using Voba.Pages;

namespace Voba
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("SavedRecipes", typeof(SavedRecipes));
            Routing.RegisterRoute("Forum", typeof(Forum));
            Routing.RegisterRoute("Recipe", typeof(Recipe));
            Routing.RegisterRoute("RecipeSelect", typeof(RecipeSelect));
            Routing.RegisterRoute("SignUp", typeof(SignUp));
        }
    }
}
