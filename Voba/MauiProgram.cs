using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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

// MongoDB Client Setup
    builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(Secrets.MongoConnectionString));

builder.Services.AddSingleton<IMongoDatabase>(sp =>
    sp.GetRequiredService<IMongoClient>()
      .GetDatabase(Secrets.MongoDatabaseName));

// Auth / security
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IAuthService, AuthService>();

// RepositoryFactory for DI & future swappability
builder.Services.AddSingleton<IUserRepository>(sp =>
    RepositoryFactory.CreateUserRepository(sp.GetRequiredService<IMongoDatabase>()));

builder.Services.AddSingleton<IGroceryListRepository>(sp =>
    RepositoryFactory.CreateGroceryListRepository(sp.GetRequiredService<IMongoDatabase>()));

builder.Services.AddSingleton<IAuthDataRepository>(sp =>
    RepositoryFactory.CreateAuthDataRepository(sp.GetRequiredService<IMongoDatabase>()));

// Service layer — external API
// FakeSpoonacularService: no API key, consistent output every run.
// Swap to SpoonacularService when Vathana's branch merges.
builder.Services.AddSingleton<ISpoonacularService, FakeSpoonacularService>();

// Service layer — orchestration, strategies, and adapters
builder.Services.AddSingleton<SpoonacularAdapter>();
builder.Services.AddSingleton<IPriceStrategy, CheapestFirstStrategy>();

// Decorator: CachedRecipeService wraps RecipeService (IMemoryCache).
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<RecipeService>();
builder.Services.AddSingleton<IRecipeService>(sp =>
    new CachedRecipeService(
        sp.GetRequiredService<RecipeService>(),
        sp.GetRequiredService<IMemoryCache>()));

builder.Services.AddSingleton<IGroceryService, GroceryService>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
