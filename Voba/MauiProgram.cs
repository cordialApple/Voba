using Microsoft.Extensions.Logging;
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

// RepositoryFactory for DI & future swappability
builder.Services.AddSingleton<IUserRepository>(sp =>
    RepositoryFactory.CreateUserRepository(sp.GetRequiredService<IMongoDatabase>()));

builder.Services.AddSingleton<IGroceryListRepository>(sp =>
    RepositoryFactory.CreateGroceryListRepository(sp.GetRequiredService<IMongoDatabase>()));

builder.Services.AddSingleton<IAuthDataRepository>(sp =>
    RepositoryFactory.CreateAuthDataRepository(sp.GetRequiredService<IMongoDatabase>()));

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
