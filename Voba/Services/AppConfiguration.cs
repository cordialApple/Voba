using System.Security.Cryptography;

namespace Voba.Services;

public static class AppConfiguration
{
    private static readonly string EphemeralJwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static string MongoConnectionString =>
        Environment.GetEnvironmentVariable("VOBA_MONGO_CONNECTION_STRING")
        ?? "mongodb://127.0.0.1:27017";

    public static string MongoDatabaseName =>
        Environment.GetEnvironmentVariable("VOBA_MONGO_DATABASE") ?? "Voba";

    public static string JwtSecret =>
        Environment.GetEnvironmentVariable("VOBA_JWT_SECRET") ?? EphemeralJwtSecret;

    public static string SpoonacularApiKey =>
        Environment.GetEnvironmentVariable("VOBA_SPOONACULAR_API_KEY") ?? string.Empty;

    public static string OllamaModel =>
        Environment.GetEnvironmentVariable("VOBA_OLLAMA_MODEL") ?? "gemma3:4b";

    public static Uri OllamaEndpoint => new(
        Environment.GetEnvironmentVariable("VOBA_OLLAMA_ENDPOINT") ?? "http://localhost:11434");

    public static bool UseFakeEnrichment =>
        (Environment.GetEnvironmentVariable("VOBA_ENRICHMENT_MODE") ?? "fake").Trim().ToLowerInvariant() switch
        {
            "fake" => true,
            "real" => false,
            _ => throw new InvalidOperationException("VOBA_ENRICHMENT_MODE must be 'fake' or 'real'.")
        };
}
