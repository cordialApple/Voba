using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Voba.Contracts;

namespace Voba.Client;

public sealed class VobaApiClient(HttpClient http, ApiSession session) : IVobaApiClient
{
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public async Task<RegisterResponse> RegisterAsync(string email, string username,
        string password, CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/api/auth/register",
            new RegisterRequest(email, username, password));
        using var response = await http.SendAsync(request, cancellationToken);
        return await ReadAsync<RegisterResponse>(response, cancellationToken);
    }

    public async Task<AuthTokensResponse> LoginAsync(string email, string password,
        CancellationToken cancellationToken = default)
    {
        using var request = Request(HttpMethod.Post, "/api/auth/login",
            new LoginRequest(email, password));
        using var response = await http.SendAsync(request, cancellationToken);
        var tokens = await ReadAsync<AuthTokensResponse>(response, cancellationToken);
        session.Set(tokens.UserId, email.Trim(), tokens.AccessToken, tokens.RefreshToken);
        return tokens;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var original = session.Snapshot();
        try
        {
            using var response = await SendAuthorizedAsync(() => Request(HttpMethod.Post,
                "/api/auth/logout"), cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
        }
        finally
        {
            if (original is not null)
                session.ClearIfEpoch(original.Epoch);
        }
    }

    public async Task<GenerationOptionsResponse> GetOptionsAsync(
        GenerationOptionsRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(() => Request(HttpMethod.Post,
            "/api/generation/options", request), cancellationToken);
        return await ReadAsync<GenerationOptionsResponse>(response, cancellationToken);
    }

    public async Task<FullRecipeResponse> SelectAsync(string draftId, string optionId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(() => Request(HttpMethod.Post,
            $"/api/generation/drafts/{Uri.EscapeDataString(draftId)}/select",
            new SelectRecipeRequest(optionId)), cancellationToken);
        return await ReadAsync<FullRecipeResponse>(response, cancellationToken);
    }

    public async Task<SavedRecipeResponse> SaveAsync(string draftId, long draftVersion,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(() => Request(HttpMethod.Post,
            "/api/recipes", new SaveRecipeRequest(draftId, draftVersion)), cancellationToken);
        return await ReadAsync<SavedRecipeResponse>(response, cancellationToken);
    }

    public async Task<IReadOnlyList<SavedRecipeResponse>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(() => Request(HttpMethod.Get,
            "/api/recipes"), cancellationToken);
        return await ReadAsync<SavedRecipeResponse[]>(response, cancellationToken);
    }

    public async Task<SavedRecipeResponse> GetAsync(string recipeId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(() => Request(HttpMethod.Get,
            $"/api/recipes/{Uri.EscapeDataString(recipeId)}"), cancellationToken);
        return await ReadAsync<SavedRecipeResponse>(response, cancellationToken);
    }

    public async Task DeleteAsync(string recipeId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(() => Request(HttpMethod.Delete,
            $"/api/recipes/{Uri.EscapeDataString(recipeId)}"), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(Func<HttpRequestMessage> create,
        CancellationToken cancellationToken)
    {
        var current = session.Snapshot() ?? throw new VobaApiException(
            HttpStatusCode.Unauthorized, "session_missing", "Sign in to continue.");
        using var first = create();
        first.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current.AccessToken);
        var response = await http.SendAsync(first, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;
        response.Dispose();

        var refreshed = await RefreshAsync(current, cancellationToken);
        using var retry = create();
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        response = await http.SendAsync(retry, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            session.ClearIfCurrent(refreshed);
        return response;
    }

    private async Task<SessionState> RefreshAsync(SessionState old,
        CancellationToken cancellationToken)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            var current = session.Snapshot();
            if (current is null || current.Epoch != old.Epoch)
                throw new VobaApiException(HttpStatusCode.Unauthorized,
                    "session_expired", "Sign in again.");
            if (current.AccessToken != old.AccessToken)
                return current;

            using var request = Request(HttpMethod.Post, "/api/auth/refresh",
                new RefreshRequest(current.RefreshToken));
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                session.ClearIfCurrent(current);
                throw new VobaApiException(HttpStatusCode.Unauthorized,
                    "session_expired", "Sign in again.");
            }
            var tokens = await ReadAsync<AuthTokensResponse>(response, cancellationToken);
            if (tokens.UserId != current.UserId)
            {
                session.ClearIfCurrent(current);
                throw new VobaApiException(HttpStatusCode.Unauthorized,
                    "session_expired", "Sign in again.");
            }
            return session.TryRotate(current, tokens.AccessToken, tokens.RefreshToken)
                ?? throw new VobaApiException(HttpStatusCode.Unauthorized,
                    "session_changed", "Sign in again.");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static HttpRequestMessage Request(HttpMethod method, string path, object? body = null) =>
        new(method, path) { Content = body is null ? null : JsonContent.Create(body) };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await EnsureSuccessAsync(response, cancellationToken);
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                ?? throw new JsonException("Response body empty.");
        }
        catch (JsonException)
        {
            throw new VobaApiException(response.StatusCode, "invalid_response",
                "Server returned an invalid response.");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;
        ApiError? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiError>(
                cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
        }
        throw new VobaApiException(response.StatusCode, error?.Code ?? "request_failed",
            error?.Message ?? $"Request failed ({(int)response.StatusCode}).");
    }
}
