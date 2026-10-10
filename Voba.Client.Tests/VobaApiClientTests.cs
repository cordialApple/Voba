using System.Net;
using System.Net.Http.Json;
using Voba.Client;
using Voba.Contracts;
using Xunit;

namespace Voba.Client.Tests;

public sealed class VobaApiClientTests
{
    [Theory]
    [InlineData("http://127.0.0.1:5057", true)]
    [InlineData("http://localhost:5057", true)]
    [InlineData("http://[::1]:5057", true)]
    [InlineData("https://api.example.com", true)]
    [InlineData("http://api.example.com", false)]
    [InlineData("ftp://localhost", false)]
    [InlineData("http://user:password@localhost:5057", false)]
    [InlineData("not-a-url", false)]
    public void Endpoint_allows_local_http_or_https_only(string value, bool allowed)
    {
        if (allowed)
            Assert.Equal(value, ApiEndpoint.Parse(value).OriginalString);
        else
            Assert.Throws<ArgumentException>(() => ApiEndpoint.Parse(value));
    }

    [Fact]
    public async Task Expired_access_token_refreshes_once_and_retries_authorized_request()
    {
        var protectedCalls = 0;
        var refreshCalls = 0;
        var handler = new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/login")
                return Json(new AuthTokensResponse("user-a", "expired-access", "refresh-one"));
            if (request.RequestUri.AbsolutePath == "/api/auth/refresh")
            {
                refreshCalls++;
                var posted = await request.Content!.ReadFromJsonAsync<RefreshRequest>();
                Assert.Equal("refresh-one", posted?.RefreshToken);
                return Json(new AuthTokensResponse("user-a", "fresh-access", "refresh-two"));
            }
            if (request.RequestUri.AbsolutePath == "/api/generation/options")
            {
                protectedCalls++;
                var bearer = request.Headers.Authorization?.Parameter;
                return bearer == "expired-access"
                    ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    : bearer == "fresh-access"
                        ? Json(new GenerationOptionsResponse("draft-1", []))
                        : throw new InvalidOperationException("Missing bearer token.");
            }
            throw new InvalidOperationException("Unexpected route.");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5058") };
        var session = new ApiSession();
        var api = new VobaApiClient(http, session);

        await api.LoginAsync("a@example.invalid", "test-password");
        var result = await api.GetOptionsAsync(new GenerationOptionsRequest(20m, 2, [], null));

        Assert.Equal("draft-1", result.DraftId);
        Assert.Equal(2, protectedCalls);
        Assert.Equal(1, refreshCalls);
        Assert.Equal("fresh-access", session.Snapshot()?.AccessToken);
        Assert.Equal("refresh-two", session.Snapshot()?.RefreshToken);
    }

    [Fact]
    public async Task Rejected_refresh_clears_local_session_and_stops_retry()
    {
        var refreshCalls = 0;
        var handler = new Handler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/generation/options")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            if (request.RequestUri.AbsolutePath == "/api/auth/refresh")
            {
                refreshCalls++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            }
            throw new InvalidOperationException("Unexpected route.");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5058") };
        var session = new ApiSession();
        session.Set("user-a", "a@example.invalid", "expired", "refresh-one");
        var api = new VobaApiClient(http, session);

        var error = await Assert.ThrowsAsync<VobaApiException>(() =>
            api.GetOptionsAsync(new GenerationOptionsRequest(20m, 2, [], null)));
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.False(session.IsAuthenticated);
        Assert.Equal(1, refreshCalls);

        await Assert.ThrowsAsync<VobaApiException>(() =>
            api.GetOptionsAsync(new GenerationOptionsRequest(20m, 2, [], null)));
        Assert.Equal(1, refreshCalls);
    }

    [Fact]
    public async Task Save_sends_draft_version_and_surfaces_stale_selection()
    {
        var handler = new Handler(async request =>
        {
            Assert.Equal("/api/recipes", request.RequestUri!.AbsolutePath);
            Assert.Equal("access", request.Headers.Authorization?.Parameter);
            var posted = await request.Content!.ReadFromJsonAsync<SaveRecipeRequest>();
            Assert.Equal("draft-a", posted?.DraftId);
            Assert.Equal(3, posted?.DraftVersion);
            return new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = JsonContent.Create(new ApiError("stale", "Recipe selection changed."))
            };
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5058") };
        var session = new ApiSession();
        session.Set("user-a", "a@example.invalid", "access", "refresh");
        var api = new VobaApiClient(http, session);

        var error = await Assert.ThrowsAsync<VobaApiException>(() => api.SaveAsync("draft-a", 3));

        Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
        Assert.Equal("stale", error.Code);
        Assert.True(session.IsAuthenticated);
    }

    [Fact]
    public async Task Logout_revokes_with_bearer_then_clears_local_tokens()
    {
        var calls = 0;
        var handler = new Handler(request =>
        {
            calls++;
            Assert.Equal("/api/auth/logout", request.RequestUri!.AbsolutePath);
            Assert.Equal("access", request.Headers.Authorization?.Parameter);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5058") };
        var session = new ApiSession();
        session.Set("user-a", "a@example.invalid", "access", "refresh");
        var api = new VobaApiClient(http, session);

        await api.LogoutAsync();
        Assert.False(session.IsAuthenticated);
        await Assert.ThrowsAsync<VobaApiException>(() => api.ListAsync());
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Refresh_finishing_after_new_login_cannot_replace_new_session()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/generation/options")
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            if (request.RequestUri.AbsolutePath == "/api/auth/refresh")
            {
                entered.SetResult();
                await release.Task;
                return Json(new AuthTokensResponse("user-a", "old-refreshed", "old-rotated"));
            }
            throw new InvalidOperationException("Unexpected route.");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5058") };
        var session = new ApiSession();
        session.Set("user-a", "a@example.invalid", "old-access", "old-refresh");
        var api = new VobaApiClient(http, session);

        var pending = api.GetOptionsAsync(new GenerationOptionsRequest(20m, 2, [], null));
        await entered.Task;
        session.Set("user-b", "b@example.invalid", "new-access", "new-refresh");
        release.SetResult();

        await Assert.ThrowsAsync<VobaApiException>(() => pending);
        Assert.Equal("user-b", session.UserId);
        Assert.Equal("new-access", session.Snapshot()?.AccessToken);
    }

    [Fact]
    public async Task Old_request_cannot_use_new_login_for_same_user()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/generation/options")
            {
                entered.SetResult();
                await release.Task;
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            throw new InvalidOperationException("Unexpected route.");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5058") };
        var session = new ApiSession();
        session.Set("user-a", "a@example.invalid", "old", "old-refresh");
        var api = new VobaApiClient(http, session);

        var pending = api.GetOptionsAsync(new GenerationOptionsRequest(20m, 2, [], null));
        await entered.Task;
        session.Set("user-a", "a@example.invalid", "new", "new-refresh");
        release.SetResult();

        await Assert.ThrowsAsync<VobaApiException>(() => pending);
        Assert.Equal("new", session.Snapshot()?.AccessToken);
    }

    [Fact]
    public async Task Concurrent_expired_requests_share_one_refresh()
    {
        var refreshCalls = 0;
        var handler = new Handler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/generation/options")
                return Task.FromResult(request.Headers.Authorization?.Parameter == "expired"
                    ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                    : Json(new GenerationOptionsResponse("draft", [])));
            if (request.RequestUri.AbsolutePath == "/api/auth/refresh")
            {
                Interlocked.Increment(ref refreshCalls);
                return Task.FromResult(Json(new AuthTokensResponse("user-a", "fresh", "rotated")));
            }
            throw new InvalidOperationException("Unexpected route.");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5058") };
        var session = new ApiSession();
        session.Set("user-a", "a@example.invalid", "expired", "refresh");
        var api = new VobaApiClient(http, session);
        var request = new GenerationOptionsRequest(20m, 2, [], null);

        var results = await Task.WhenAll(api.GetOptionsAsync(request), api.GetOptionsAsync(request));

        Assert.All(results, result => Assert.Equal("draft", result.DraftId));
        Assert.Equal(1, refreshCalls);
    }

    [Fact]
    public async Task Logout_during_refresh_cannot_restore_session()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/generation/options")
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            if (request.RequestUri.AbsolutePath == "/api/auth/refresh")
            {
                entered.SetResult();
                await release.Task;
                return Json(new AuthTokensResponse("user-a", "fresh", "rotated"));
            }
            throw new InvalidOperationException("Unexpected route.");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5058") };
        var session = new ApiSession();
        session.Set("user-a", "a@example.invalid", "old", "refresh");
        var api = new VobaApiClient(http, session);

        var pending = api.GetOptionsAsync(new GenerationOptionsRequest(20m, 2, [], null));
        await entered.Task;
        session.Clear();
        release.SetResult();

        await Assert.ThrowsAsync<VobaApiException>(() => pending);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public async Task Stale_retry_denial_cannot_clear_new_login()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/generation/options")
            {
                if (request.Headers.Authorization?.Parameter == "old")
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized);
                entered.SetResult();
                await release.Task;
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            if (request.RequestUri.AbsolutePath == "/api/auth/refresh")
                return Json(new AuthTokensResponse("user-a", "fresh", "rotated"));
            throw new InvalidOperationException("Unexpected route.");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:5058") };
        var session = new ApiSession();
        session.Set("user-a", "a@example.invalid", "old", "refresh");
        var api = new VobaApiClient(http, session);

        var pending = api.GetOptionsAsync(new GenerationOptionsRequest(20m, 2, [], null));
        await entered.Task;
        session.Set("user-b", "b@example.invalid", "new", "new-refresh");
        release.SetResult();

        await Assert.ThrowsAsync<VobaApiException>(() => pending);
        Assert.Equal("user-b", session.UserId);
        Assert.Equal("new", session.Snapshot()?.AccessToken);
    }

    private static HttpResponseMessage Json<T>(T body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => send(request);
    }
}
