namespace Voba.Models;

public sealed record RecipeGenerationCacheEntry(
    string Key,
    RecipeDataSource Source,
    string PayloadJson,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);
