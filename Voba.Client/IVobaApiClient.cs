using Voba.Contracts;

namespace Voba.Client;

public interface IVobaApiClient
{
    Task<RegisterResponse> RegisterAsync(string email, string username, string password,
        CancellationToken cancellationToken = default);
    Task<AuthTokensResponse> LoginAsync(string email, string password,
        CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<GenerationOptionsResponse> GetOptionsAsync(GenerationOptionsRequest request,
        CancellationToken cancellationToken = default);
    Task<FullRecipeResponse> SelectAsync(string draftId, string optionId,
        CancellationToken cancellationToken = default);
    Task<SavedRecipeResponse> SaveAsync(string draftId, long draftVersion,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SavedRecipeResponse>> ListAsync(
        CancellationToken cancellationToken = default);
    Task<SavedRecipeResponse> GetAsync(string recipeId,
        CancellationToken cancellationToken = default);
    Task DeleteAsync(string recipeId, CancellationToken cancellationToken = default);
}
