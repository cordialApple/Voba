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

            // Spoonacular Service
            builder.Services.AddSingleton<Spoonacular.SpoonacularService>();

            // Pipeline Handlers
            // Registered as Transient so each page resolution gets a fresh chain.
            // Chain order: Ideation → Pricing → FullRecipe
            builder.Services.AddTransient<AI.Pipeline.Handlers.GemmaIdeationHandler>();
            builder.Services.AddTransient<AI.Pipeline.Handlers.SpoonacularPricingHandler>();
            builder.Services.AddTransient<AI.Pipeline.Handlers.GemmaFullRecipeHandler>();

            //Gemma AI Services
            builder.Services.AddSingleton<Services.IAiChatService, Services.SemanticKernelChatService>();
            builder.Services.AddTransient<MainPage>();

#if DEBUG
            builder.Logging.AddDebug();

#endif

            return builder.Build();
        }
    }
}
