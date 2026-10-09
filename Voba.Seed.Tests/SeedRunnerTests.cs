using Voba.Interfaces;
using Voba.Models;
using Voba.Services;
using Xunit;

namespace Voba.Seed.Tests;

public sealed class SeedRunnerTests
{
    [Theory]
    [InlineData("Voba")]
    [InlineData("VobaDemo")]
    [InlineData("production")]
    public void Refuses_non_test_database(string databaseName) =>
        Assert.Throws<InvalidOperationException>(() => SeedRunner.ValidateDatabaseName(databaseName));

    [Theory]
    [InlineData("VobaDemoTests")]
    [InlineData("voba_seed_123")]
    public void Accepts_explicit_test_database(string databaseName) =>
        SeedRunner.ValidateDatabaseName(databaseName);

    [Fact]
    public async Task Repeated_run_keeps_two_users_two_recipes_and_same_cache_keys()
    {
        var auth = new MemoryAuth();
        var recipes = new MemoryRecipes();
        var cache = new MemoryCache();

        await SeedRunner.RunAsync(auth, recipes, cache);
        var firstKeys = cache.Keys.Order(StringComparer.Ordinal).ToArray();
        await SeedRunner.RunAsync(auth, recipes, cache);

        Assert.Equal(2, auth.RegisteredUsers);
        Assert.Equal(2, recipes.SavedCount);
        Assert.Equal(firstKeys, cache.Keys.Order(StringComparer.Ordinal));
        Assert.All(recipes.Saved, recipe => Assert.Equal(RecipeDataSource.Synthetic, recipe.DataSource));
    }

    [Fact]
    public async Task Existing_account_with_different_password_aborts_before_fixture_write()
    {
        var auth = new MemoryAuth { RejectLogin = true };
        var recipes = new MemoryRecipes();
        var cache = new MemoryCache();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SeedRunner.RunAsync(auth, recipes, cache));

        Assert.Equal(0, recipes.SavedCount);
        Assert.Empty(cache.Keys);
    }

    [Fact]
    public async Task Existing_duplicate_fixture_recipes_fail_verification()
    {
        var auth = new MemoryAuth();
        var recipes = new MemoryRecipes();
        var cache = new MemoryCache();
        await SeedRunner.RunAsync(auth, recipes, cache);
        recipes.DuplicateFirst();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SeedRunner.RunAsync(auth, recipes, cache));
    }

    private sealed class MemoryAuth : IBackendAuthService
    {
        private readonly Dictionary<string, User> _users = [];
        public bool RejectLogin { get; set; }
        public int RegisteredUsers => _users.Count;

        public Task<ServiceResult<User>> RegisterAsync(string email, string username, string password,
            CancellationToken cancellationToken = default)
        {
            if (RejectLogin || _users.ContainsKey(email))
                return Task.FromResult(ServiceResult<User>.Fail(ErrorCodes.ValidationError,
                    "Email already registered."));
            var user = new User(email, username);
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(user,
                MongoDB.Bson.ObjectId.GenerateNewId().ToString());
            _users[email] = user;
            return Task.FromResult(ServiceResult<User>.Ok(user));
        }

        public Task<ServiceResult<AuthTokens>> LoginAsync(string email, string password,
            CancellationToken cancellationToken = default) => Task.FromResult(
                !RejectLogin && _users.TryGetValue(email, out var user)
                    ? ServiceResult<AuthTokens>.Ok(new AuthTokens(user.Id, "refresh", user.Id))
                    : ServiceResult<AuthTokens>.Fail(ErrorCodes.Unauthorized, "Invalid credentials."));

        public Task<ServiceResult<AuthTokens>> RefreshAsync(string rawRefreshToken,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<BackendPrincipal?> AuthenticateAsync(string accessJwt,
            CancellationToken cancellationToken = default) => Task.FromResult<BackendPrincipal?>(
                _users.Values.Where(user => user.Id == accessJwt)
                    .Select(user => new BackendPrincipal(user.Id, "session")).FirstOrDefault());

        public Task<bool> LogoutAsync(BackendPrincipal principal,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class MemoryRecipes : IUserRecipeStore
    {
        private readonly List<Recipe> _recipes = [];
        public int SavedCount => _recipes.Count;
        public IReadOnlyList<Recipe> Saved => _recipes;
        public void DuplicateFirst() => _recipes.Add(_recipes[0]);

        public Task<Recipe> SaveAsync(string userId, RecipeGenerationContext context,
            CancellationToken cancellationToken = default)
        {
            var recipe = RecipeMapper.ToRecipe(context, userId);
            _recipes.Add(recipe);
            return Task.FromResult(recipe);
        }

        public Task<List<Recipe>> ListAsync(string userId,
            CancellationToken cancellationToken = default) => Task.FromResult(
            _recipes.Where(recipe => recipe.UserId == userId).ToList());

        public Task<Recipe?> GetAsync(string recipeId, string userId,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<bool> DeleteAsync(string recipeId, string userId,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class MemoryCache : IRecipeGenerationCache
    {
        private readonly Dictionary<string, RecipeGenerationCacheEntry> _entries = [];
        public IEnumerable<string> Keys => _entries.Keys;

        public Task<RecipeGenerationCacheEntry?> GetAsync(string key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_entries.GetValueOrDefault(key));

        public Task<RecipeGenerationCacheEntry> StoreAsync(RecipeGenerationCacheEntry entry,
            CancellationToken cancellationToken = default)
        {
            if (!_entries.TryGetValue(entry.Key, out var winner) || entry.Source >= winner.Source)
                _entries[entry.Key] = entry;
            return Task.FromResult(_entries[entry.Key]);
        }
    }
}
