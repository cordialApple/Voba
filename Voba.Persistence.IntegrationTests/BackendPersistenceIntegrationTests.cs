using System.Security.Cryptography;
using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;
using Voba.Repositories;
using Voba.Services;
using Xunit;

namespace Voba.Persistence.Tests;

public sealed class BackendPersistenceIntegrationTests
{
    [Fact]
    public async Task Failed_second_registration_write_rolls_back_user()
    {
        await WithDatabaseAsync(async (database, suffix) =>
        {
            var accounts = new MongoBackendAccountStore(database,
                $"users_{suffix}", $"authdata_{suffix}");
            var user = new User($"rollback-{suffix}@example.invalid", "Rollback");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                accounts.CreateAsync(user, "long enough password", new FailingHasher()));

            Assert.Null(await accounts.GetUserByEmailAsync(user.Email));
        });
    }

    [Fact]
    public async Task Concurrent_case_variant_registration_creates_one_account()
    {
        await WithDatabaseAsync(async (database, suffix) =>
        {
            var accounts = new MongoBackendAccountStore(database,
                $"users_{suffix}", $"authdata_{suffix}");
            var email = $"race-{suffix}@example.invalid";
            var writes = await Task.WhenAll(
                TryCreateAsync(accounts, new User(email.ToUpperInvariant(), "First")),
                TryCreateAsync(accounts, new User($" {email} ", "Second")));

            Assert.Equal(1, writes.Count(success => success));
            var user = await accounts.GetUserByEmailAsync(email);
            Assert.NotNull(user);
            Assert.NotNull(await accounts.GetAuthDataByUserIdAsync(user.Id));
        });
    }

    [Fact]
    public async Task Concurrent_refresh_rotation_has_one_winner_and_logout_revokes_session()
    {
        await WithDatabaseAsync(async (database, suffix) =>
        {
            var sessions = new MongoBackendSessionStore(database, $"sessions_{suffix}");
            var now = DateTime.UtcNow;
            var id = Guid.NewGuid().ToString("N");
            await sessions.CreateAsync(new BackendSession(id, "user-id", "old-hash", now.AddHours(1)));

            var results = await Task.WhenAll(
                sessions.RotateAsync("old-hash", "new-hash-1", now, now.AddDays(1)),
                sessions.RotateAsync("old-hash", "new-hash-2", now, now.AddDays(1)));

            Assert.Single(results.Where(result => result is not null));
            Assert.True(await sessions.IsActiveAsync(id, "user-id", now));
            Assert.False(await sessions.IsActiveAsync(id, "other-user", now));
            Assert.True(await sessions.RevokeAsync(id, "user-id"));
            Assert.False(await sessions.IsActiveAsync(id, "user-id", now));
            Assert.Null(await sessions.RotateAsync(results.First(result => result is not null)!.RefreshHash,
                "after-logout", now, now.AddDays(1)));
        });
    }

    [Fact]
    public async Task Expired_session_cannot_authenticate_or_rotate()
    {
        await WithDatabaseAsync(async (database, suffix) =>
        {
            var sessions = new MongoBackendSessionStore(database, $"sessions_{suffix}");
            var now = DateTime.UtcNow;
            var id = Guid.NewGuid().ToString("N");
            await sessions.CreateAsync(new BackendSession(id, "user-id", "expired-hash",
                now.AddSeconds(-1)));

            Assert.False(await sessions.IsActiveAsync(id, "user-id", now));
            Assert.Null(await sessions.RotateAsync("expired-hash", "new-hash",
                now, now.AddDays(1)));
        });
    }

    [Fact]
    public async Task Fresh_service_instance_accepts_session_then_logout_revokes_it()
    {
        await WithDatabaseAsync(async (database, suffix) =>
        {
            var accountsName = $"users_{suffix}";
            var authName = $"authdata_{suffix}";
            var sessionName = $"sessions_{suffix}";
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            BackendAuthService CreateService() => new(
                new MongoBackendAccountStore(database, accountsName, authName),
                new MongoBackendSessionStore(database, sessionName),
                new BackendPasswordHasher(),
                new BackendTokenService(key, "voba-test", "voba-client"),
                TimeProvider.System);

            var first = CreateService();
            Assert.True((await first.RegisterAsync($"restart-{suffix}@example.invalid",
                "Restart", "long enough password")).Success);
            var login = await first.LoginAsync($"restart-{suffix}@example.invalid",
                "long enough password");
            Assert.True(login.Success);

            var restarted = CreateService();
            var principal = await restarted.AuthenticateAsync(login.Data!.AccessToken);
            Assert.NotNull(principal);
            Assert.True(await restarted.LogoutAsync(principal!));
            Assert.Null(await first.AuthenticateAsync(login.Data.AccessToken));
            Assert.False((await first.RefreshAsync(login.Data.RefreshToken)).Success);
        });
    }

    [Fact]
    public async Task Draft_requires_owner_and_current_version()
    {
        await WithDatabaseAsync(async (database, suffix) =>
        {
            var drafts = new MongoGenerationDraftStore(database, $"drafts_{suffix}");
            var context = new RecipeGenerationContext { ServingSize = 2, TargetBudget = 20m };
            var draft = await drafts.CreateAsync("user-a", context, DateTime.UtcNow.AddHours(1), default);

            Assert.Null(await drafts.GetAsync(draft.Id, "user-b", default));
            Assert.True(await drafts.ReplaceAsync(draft, new RecipeGenerationContext
            {
                ServingSize = 4, TargetBudget = 20m
            }, default));
            Assert.False(await drafts.ReplaceAsync(draft, context, default));
            Assert.Equal(4, (await drafts.GetAsync(draft.Id, "user-a", default))!.Context.ServingSize);
        });
    }

    [Fact]
    public async Task Saved_recipe_read_and_delete_require_owner()
    {
        await WithDatabaseAsync(async (database, suffix) =>
        {
            var recipes = new MongoUserRecipeStore(database, $"recipes_{suffix}");
            var context = new RecipeGenerationContext
            {
                SelectedOption = new RecipeOption { Name = "Soup", Ingredients = ["beans"] },
                FinalRecipe = new FullRecipe { Title = "Soup", Instructions = "1. Cook beans." }
            };
            var saved = await recipes.SaveAsync("507f1f77bcf86cd799439011", context);

            Assert.Null(await recipes.GetAsync(saved.Id, "507f1f77bcf86cd799439012"));
            Assert.Empty(await recipes.ListAsync("507f1f77bcf86cd799439012"));
            Assert.False(await recipes.DeleteAsync(saved.Id, "507f1f77bcf86cd799439012"));
            Assert.Equal(saved.Id, (await recipes.GetAsync(saved.Id, "507f1f77bcf86cd799439011"))?.Id);
            Assert.True(await recipes.DeleteAsync(saved.Id, "507f1f77bcf86cd799439011"));
        });
    }

    private static async Task<bool> TryCreateAsync(MongoBackendAccountStore accounts, User user)
    {
        try
        {
            await accounts.CreateAsync(user, "long enough password", new TestHasher());
            return true;
        }
        catch (MongoWriteException error) when (error.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
        catch (MongoCommandException error) when (error.Code == 11000)
        {
            return false;
        }
    }

    private static async Task WithDatabaseAsync(Func<IMongoDatabase, string, Task> test)
    {
        var uri = Environment.GetEnvironmentVariable("VOBA_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri))
            throw new InvalidOperationException("Set VOBA_TEST_MONGO_URI to run isolated Mongo integration tests.");
        var databaseName = Environment.GetEnvironmentVariable("VOBA_TEST_MONGO_DATABASE") ?? "VobaDemoTests";
        var database = new MongoClient(uri).GetDatabase(databaseName);
        var suffix = Guid.NewGuid().ToString("N");
        try
        {
            await test(database, suffix);
        }
        finally
        {
            foreach (var prefix in new[] { "users", "authdata", "sessions", "drafts", "recipes" })
                await database.DropCollectionAsync($"{prefix}_{suffix}");
        }
    }

    private sealed class TestHasher : IPasswordHasher
    {
        public string GenerateSalt() => "salt";
        public string Hash(string plainText, string salt) => $"{salt}:{plainText}";
        public bool Verify(string plainText, string hash) => hash == Hash(plainText, "salt");
    }

    private sealed class FailingHasher : IPasswordHasher
    {
        public string GenerateSalt() => throw new InvalidOperationException("Injected password write failure.");
        public string Hash(string plainText, string salt) => throw new NotImplementedException();
        public bool Verify(string plainText, string hash) => throw new NotImplementedException();
    }
}
