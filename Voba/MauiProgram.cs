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
                modelId: "gemma3:1b", 
                endpoint: new Uri("http://localhost:11434")
            );

#if DEBUG
            builder.Logging.AddDebug();

#endif
            builder.Services.AddSingleton<Voba.Services.IAiChatService, Voba.Services.SemanticKernelChatService>();
            builder.Services.AddTransient<MainPage>();

            return builder.Build();
        }
    }
}
