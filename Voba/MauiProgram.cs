using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Repositories;
using Voba.Services;

namespace Voba
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            spoonacular.api.ApiSettings.SpoonacularApiKey = AppConfiguration.SpoonacularApiKey;

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<IMongoClient>(sp =>
            {
                var settings = MongoClientSettings.FromConnectionString(AppConfiguration.MongoConnectionString);
                settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
                return new MongoClient(settings);
            });

            builder.Services.AddSingleton<IMongoDatabase>(sp =>
                sp.GetRequiredService<IMongoClient>()
                  .GetDatabase(AppConfiguration.MongoDatabaseName));

            builder.Services.AddSingleton<IRecipeGenerationCache, MongoRecipeGenerationCache>();
            builder.Services.AddSingleton(sp => new RecipeGenerationCoordinator(
                sp.GetRequiredService<IRecipeGenerationCache>(),
                TimeProvider.System,
                TimeSpan.FromHours(24),
                AppConfiguration.OllamaModel,
                "recipe-prompts-v1"));

            builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
            builder.Services.AddSingleton<IJwtService, JwtService>();
            builder.Services.AddSingleton<IAuthService, AuthService>();
            builder.Services.AddSingleton<ICurrentUserService, CurrentUserService>();

            builder.Services.AddSingleton<IUserRepository>(sp =>
                RepositoryFactory.CreateUserRepository(sp.GetRequiredService<IMongoDatabase>()));

            builder.Services.AddSingleton<IAuthDataRepository>(sp =>
                RepositoryFactory.CreateAuthDataRepository(sp.GetRequiredService<IMongoDatabase>()));

            builder.Services.AddSingleton<IRecipeRepository>(sp =>
                RepositoryFactory.CreateRecipeRepository(sp.GetRequiredService<IMongoDatabase>()));

            builder.Services.AddSingleton<IIngredientRepository>(sp =>
                RepositoryFactory.CreateIngredientRepository(sp.GetRequiredService<IMongoDatabase>()));

            // Spoonacular SDK wrapper used by the live enrichment provider.
            builder.Services.AddSingleton<Spoonacular.SpoonacularService>();

            if (AppConfiguration.UseFakeEnrichment)
                builder.Services.AddSingleton<IRecipeEnrichmentService, FakeEnrichmentService>();
            else
            {
                if (string.IsNullOrWhiteSpace(AppConfiguration.SpoonacularApiKey))
                    throw new InvalidOperationException("VOBA_SPOONACULAR_API_KEY is required in real enrichment mode.");
                builder.Services.AddSingleton<IRecipeEnrichmentService, SpoonacularEnrichmentService>();
            }

            // Gemma via Ollama for recipe ideation + instructions.
            builder.Services.AddKernel()
            .AddOllamaChatCompletion(
                modelId: AppConfiguration.OllamaModel,
                endpoint: AppConfiguration.OllamaEndpoint
            );

            builder.Services.AddSingleton<Services.IAiChatService, Services.SemanticKernelChatService>();

            // Recipe generation pipeline handlers.
            builder.Services.AddTransient<AI.Pipeline.Handlers.GemmaIdeationHandler>();
            builder.Services.AddTransient<AI.Pipeline.Handlers.SpoonacularPricingHandler>();
            builder.Services.AddTransient<AI.Pipeline.Handlers.GemmaFullRecipeHandler>();

            // Pages — every navigable page is registered so Shell can resolve
            // constructor dependencies via DI.
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
}
