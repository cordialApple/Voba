using Voba.Models;

namespace Voba.Interfaces;

public interface IGenerationDraftStore
{
    Task<GenerationDraft> CreateAsync(string userId, RecipeGenerationContext context,
        DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    Task<GenerationDraft?> GetAsync(string id, string userId,
        CancellationToken cancellationToken = default);

    Task<bool> ReplaceAsync(GenerationDraft original, RecipeGenerationContext context,
        CancellationToken cancellationToken = default);
}
