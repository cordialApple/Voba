using MongoDB.Driver;
using Voba.Models;
using Voba.Repositories;
using Xunit;

namespace Voba.Persistence.Tests;

public sealed class MongoRecipeGenerationCacheIntegrationTests
{
    [Fact]
    public async Task RealEntrySurvivesLaterSyntheticWriteAndExpiry()
    {
        await WithCacheAsync(async cache =>
        {
            var key = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow;
            var synthetic = new RecipeGenerationCacheEntry(key, RecipeDataSource.Synthetic, "synthetic", now, now.AddMinutes(1));
            var real = new RecipeGenerationCacheEntry(key, RecipeDataSource.Real, "real", now, now.AddMilliseconds(-1));

            await cache.StoreAsync(synthetic);
            Assert.Equal("real", (await cache.StoreAsync(real)).PayloadJson);
            Assert.Null(await cache.GetAsync(key));
            Assert.Equal("real", (await cache.StoreAsync(synthetic)).PayloadJson);
        });
    }

    [Fact]
    public async Task SameSourceRefreshesExpiredEntry()
    {
        await WithCacheAsync(async cache =>
        {
            var key = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow;
            var expired = new RecipeGenerationCacheEntry(key, RecipeDataSource.Synthetic, "old", now.AddDays(-1), now.AddSeconds(-1));
            var fresh = new RecipeGenerationCacheEntry(key, RecipeDataSource.Synthetic, "new", now, now.AddHours(1));

            await cache.StoreAsync(expired);
            Assert.Null(await cache.GetAsync(key));
            Assert.Equal("new", (await cache.StoreAsync(fresh)).PayloadJson);
            Assert.Equal("new", (await cache.GetAsync(key))?.PayloadJson);
        });
    }

    [Fact]
    public async Task ConcurrentSyntheticAndRealWritesLeaveRealAuthoritative()
    {
        await WithCacheAsync(async cache =>
        {
            var now = DateTime.UtcNow;
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var key = Guid.NewGuid().ToString("N");
                var synthetic = new RecipeGenerationCacheEntry(key, RecipeDataSource.Synthetic, "synthetic", now, now.AddHours(1));
                var real = new RecipeGenerationCacheEntry(key, RecipeDataSource.Real, "real", now, now.AddHours(1));

                await Task.WhenAll(cache.StoreAsync(synthetic), cache.StoreAsync(real));

                var winner = await cache.GetAsync(key);
                Assert.Equal(RecipeDataSource.Real, winner?.Source);
                Assert.Equal("real", winner?.PayloadJson);
            }
        });
    }

    [Fact]
    public async Task CancelledWriteDoesNotReachDatabase()
    {
        await WithCacheAsync(async cache =>
        {
            var now = DateTime.UtcNow;
            var entry = new RecipeGenerationCacheEntry(Guid.NewGuid().ToString("N"), RecipeDataSource.Real, "real", now, now.AddHours(1));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.StoreAsync(entry, cancellation.Token));
            Assert.Null(await cache.GetAsync(entry.Key));
        });
    }

    private static async Task WithCacheAsync(Func<MongoRecipeGenerationCache, Task> test)
    {
        var uri = Environment.GetEnvironmentVariable("VOBA_TEST_MONGO_URI");
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new InvalidOperationException("Set VOBA_TEST_MONGO_URI to run isolated Mongo integration tests.");
        }

        var client = new MongoClient(uri);
        var databaseName = Environment.GetEnvironmentVariable("VOBA_TEST_MONGO_DATABASE") ?? "VobaDemoTests";
        var collectionName = $"recipe_cache_test_{Guid.NewGuid():N}";
        var database = client.GetDatabase(databaseName);
        try
        {
            await test(new MongoRecipeGenerationCache(database, collectionName));
        }
        finally
        {
            await database.DropCollectionAsync(collectionName);
        }
    }
}
