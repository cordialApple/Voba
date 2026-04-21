using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace Voba
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddKernel()
            .AddOllamaChatCompletion(
                modelId: "gemma3:4b",
                endpoint: new Uri("http://localhost:11434")
            );

            // Pipeline Handlers
            // Chain order: Ideation → FullRecipe (Spoonacular pricing removed)
            builder.Services.AddTransient<AI.Pipeline.Handlers.GemmaIdeationHandler>();
            builder.Services.AddTransient<AI.Pipeline.Handlers.GemmaFullRecipeHandler>();

            // Gemma AI Services
            builder.Services.AddSingleton<Services.IAiChatService, Services.SemanticKernelChatService>();

            // Pages
            builder.Services.AddTransient<Pages.Login>();
            builder.Services.AddTransient<Pages.SignUp>();
            builder.Services.AddTransient<Pages.Home>();
            builder.Services.AddTransient<Pages.Forum>();
            builder.Services.AddTransient<Pages.RecipeSelect>();
            builder.Services.AddTransient<Pages.Recipe>();
            builder.Services.AddTransient<Pages.SavedRecipes>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}