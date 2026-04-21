namespace Voba
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("Recipe", typeof(Pages.Recipe));
        }
    }
}
