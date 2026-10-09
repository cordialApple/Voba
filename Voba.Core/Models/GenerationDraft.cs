namespace Voba.Models;

public sealed record GenerationDraft(
    string Id,
    string UserId,
    RecipeGenerationContext Context,
    DateTime ExpiresAtUtc,
    long Version);
