using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using MongoDB.Driver;
using Voba.api;
using Voba.Interfaces;
using Voba.Repositories;
using Voba.Services;

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

    builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(Secrets.MongoConnectionString));

builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>()
      .GetDatabase(Secrets.MongoDatabaseName));

builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IAuthService, AuthService>();

builder.Services.AddSingleton<IUserRepository>(sp =>
    RepositoryFactory.CreateUserRepository(sp.GetRequiredService<IMongoDatabase>()));

builder.Services.AddSingleton<IGroceryListRepository>(sp =>
    RepositoryFactory.CreateGroceryListRepository(sp.GetRequiredService<IMongoDatabase>()));

builder.Services.AddSingleton<IAuthDataRepository>(sp =>
    RepositoryFactory.CreateAuthDataRepository(sp.GetRequiredService<IMongoDatabase>()));

builder.Services.AddSingleton<IRecipeRepository>(sp =>
    RepositoryFactory.CreateRecipeRepository(sp.GetRequiredService<IMongoDatabase>()));

builder.Services.AddSingleton<IIngredientRepository>(sp =>
    RepositoryFactory.CreateIngredientRepository(sp.GetRequiredService<IMongoDatabase>()));

builder.Services.AddSingleton<ISpoonacularService, FakeSpoonacularService>();

builder.Services.AddSingleton<SpoonacularAdapter>();
builder.Services.AddSingleton<IPriceStrategy, CheapestFirstStrategy>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<RecipeService>();
builder.Services.AddSingleton<IRecipeService>(sp =>
    new CachedRecipeService(
        sp.GetRequiredService<RecipeService>(),
        sp.GetRequiredService<IMemoryCache>()));

builder.Services.AddSingleton<IGroceryService, GroceryService>();
            builder.Services.AddKernel()
            .AddOllamaChatCompletion(
                modelId: "gemma3:4b",
                endpoint: new Uri("http://localhost:11434")
            );

            builder.Services.AddSingleton<Spoonacular.SpoonacularService>();

            builder.Services.AddTransient<AI.Pipeline.Handlers.GemmaIdeationHandler>();
            builder.Services.AddTransient<AI.Pipeline.Handlers.SpoonacularPricingHandler>();
            builder.Services.AddTransient<AI.Pipeline.Handlers.GemmaFullRecipeHandler>();

            builder.Services.AddSingleton<Services.IAiChatService, Services.SemanticKernelChatService>();
            builder.Services.AddTransient<Pages.Login>();
            builder.Services.AddTransient<Pages.SignUp>();
            builder.Services.AddTransient<Pages.RecipeSelect>();

#if DEBUG
            builder.Logging.AddDebug();

#endif

            return builder.Build();
        }
    }
}
