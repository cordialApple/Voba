using System.Security.Cryptography;
using MongoDB.Driver;
using Voba.Repositories;
using Voba.Services;

namespace Voba.Seed;

internal static class Program
{
    private static async Task Main()
    {
        var databaseName = Environment.GetEnvironmentVariable("VOBA_TEST_MONGO_DATABASE")
            ?? throw new InvalidOperationException("VOBA_TEST_MONGO_DATABASE is required.");
        SeedRunner.ValidateDatabaseName(databaseName);
        var uri = Environment.GetEnvironmentVariable("VOBA_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri))
            throw new InvalidOperationException("VOBA_TEST_MONGO_URI is required.");

        var settings = MongoClientSettings.FromConnectionString(uri);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(15);
        var database = new MongoClient(settings).GetDatabase(databaseName);
        var accounts = new MongoBackendAccountStore(database);
        var sessions = new MongoBackendSessionStore(database);
        var tokens = new BackendTokenService(
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            "voba-seed", "voba-seed");
        var auth = new BackendAuthService(accounts, sessions,
            new BackendPasswordHasher(), tokens, TimeProvider.System);
        await SeedRunner.RunAsync(auth, new MongoUserRecipeStore(database),
            new MongoRecipeGenerationCache(database));
        Console.WriteLine("Seed verified: 2 dummy accounts, 2 synthetic saved recipes, 2 generation cache entries.");
    }
}
