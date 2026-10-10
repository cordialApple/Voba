using Microsoft.Extensions.Logging;
using Voba.Client;

namespace Voba;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });

        var baseUrl = Environment.GetEnvironmentVariable("VOBA_API_BASE_URL")
            ?? "http://127.0.0.1:5057";
        var endpoint = ApiEndpoint.Parse(baseUrl);
        builder.Services.AddSingleton(new HttpClient
        {
            BaseAddress = endpoint,
            Timeout = TimeSpan.FromMinutes(5)
        });
        builder.Services.AddSingleton<ApiSession>();
        builder.Services.AddSingleton<IVobaApiClient, VobaApiClient>();

        builder.Services.AddTransient<Pages.Login>();
        builder.Services.AddTransient<Pages.SignUp>();
        builder.Services.AddTransient<Pages.Home>();
        builder.Services.AddTransient<Pages.Hub>();
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
