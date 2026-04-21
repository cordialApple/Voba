namespace Voba
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
                Routing.RegisterRoute(nameof(Pages.SignUp), typeof(Pages.SignUp));
                Routing.RegisterRoute(nameof(Pages.Login), typeof(Pages.Login));
                Routing.RegisterRoute(nameof(Pages.RecipeSelect), typeof(Pages.RecipeSelect));
        }
    }
}
