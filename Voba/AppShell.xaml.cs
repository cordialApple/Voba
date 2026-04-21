namespace Voba
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
                Routing.RegisterRoute(nameof(Pages.SignUp), typeof(Pages.SignUp));
                Routing.RegisterRoute(nameof(Pages.Login), typeof(Pages.Login));
                Routing.RegisterRoute(nameof(Pages.Home), typeof(Pages.Home));
                Routing.RegisterRoute(nameof(Pages.Hub), typeof(Pages.Hub));
                Routing.RegisterRoute(nameof(Pages.Forum), typeof(Pages.Forum));
                Routing.RegisterRoute(nameof(Pages.Recipe), typeof(Pages.Recipe));
                Routing.RegisterRoute(nameof(Pages.RecipeSelect), typeof(Pages.RecipeSelect));
                Routing.RegisterRoute(nameof(Pages.SavedRecipes), typeof(Pages.SavedRecipes));
        }
    }
}
