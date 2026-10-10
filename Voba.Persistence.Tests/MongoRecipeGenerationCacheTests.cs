using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Connections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Voba.Models;
using Voba.Repositories;
using Xunit;

namespace Voba.Persistence.Tests;

public sealed class MongoRecipeGenerationCacheTests
{
    [Fact]
    public void ConstructorRejectsBlankCollectionName()
    {
        var database = new MongoClient("mongodb://127.0.0.1:27017").GetDatabase("cache_tests");

        Assert.Throws<ArgumentException>(() => new MongoRecipeGenerationCache(database, " "));
    }

    [Fact]
    public void ReadFilterRejectsExpiredEntries()
    {
        var now = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
        var filter = MongoRecipeGenerationCache.CreateReadFilter("recipe-key", now);
        var document = filter.Render(new RenderArgs<BsonDocument>(BsonDocumentSerializer.Instance, BsonSerializer.SerializerRegistry));

        Assert.Equal("recipe-key", document["_id"].AsString);
        Assert.Equal(now, document["expiresAtUtc"]["$gt"].ToUniversalTime());
    }

    [Theory]
    [InlineData(RecipeDataSource.Estimate, 0)]
    [InlineData(RecipeDataSource.Synthetic, 1)]
    [InlineData(RecipeDataSource.Real, 2)]
    public void StoreFilterRequiresExistingSourceAtOrBelowIncoming(RecipeDataSource source, int expectedRank)
    {
        var filter = MongoRecipeGenerationCache.CreateStoreFilter("recipe-key", source);
        var document = filter.Render(new RenderArgs<BsonDocument>(BsonDocumentSerializer.Instance, BsonSerializer.SerializerRegistry));

        Assert.Equal("recipe-key", document["_id"].AsString);
        Assert.Equal(expectedRank, document["source"]["$lte"].AsInt32);
        Assert.DoesNotContain("expiresAtUtc", document.ToJson());
    }

    [Fact]
    public async Task DuplicateUpsertRetriesConditionalUpdateAndReturnsWinner()
    {
        var now = DateTime.UtcNow;
        var attemptedUpserts = new List<bool>();
        var real = new RecipeGenerationCacheEntry("recipe-key", RecipeDataSource.Real, "real", now, now.AddHours(1));
        var cache = new MongoRecipeGenerationCache(
            (filter, update, upsert, cancellationToken) =>
            {
                attemptedUpserts.Add(upsert);
                if (upsert)
                {
                    throw new MongoWriteException((ConnectionId)RuntimeHelpers.GetUninitializedObject(typeof(ConnectionId)), DuplicateKeyError(), null, null);
                }

                var rendered = filter.Render(new RenderArgs<BsonDocument>(BsonDocumentSerializer.Instance, BsonSerializer.SerializerRegistry));
                Assert.Equal((int)RecipeDataSource.Synthetic, rendered["source"]["$lte"].AsInt32);
                return Task.CompletedTask;
            },
            (filter, cancellationToken) => Task.FromResult<BsonDocument?>(ToDocument(real)));

        var incoming = real with { Source = RecipeDataSource.Synthetic, PayloadJson = "synthetic" };
        var winner = await cache.StoreAsync(incoming);

        Assert.Equal(new[] { true, false }, attemptedUpserts);
        Assert.Equal(real.Key, winner.Key);
        Assert.Equal(real.Source, winner.Source);
        Assert.Equal(real.PayloadJson, winner.PayloadJson);
    }

    [Fact]
    public async Task CancelledWriteNeverInvokesDriver()
    {
        var updates = 0;
        var cache = new MongoRecipeGenerationCache(
            (filter, update, upsert, cancellationToken) =>
            {
                updates++;
                return Task.CompletedTask;
            },
            (filter, cancellationToken) => Task.FromResult<BsonDocument?>(null));
        var now = DateTime.UtcNow;
        var entry = new RecipeGenerationCacheEntry("recipe-key", RecipeDataSource.Real, "real", now, now.AddHours(1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.StoreAsync(entry, cancellation.Token));
        Assert.Equal(0, updates);
    }

    private static BsonDocument ToDocument(RecipeGenerationCacheEntry entry) => new()
    {
        { "_id", entry.Key },
        { "source", (int)entry.Source },
        { "payloadJson", entry.PayloadJson },
        { "createdAtUtc", entry.CreatedAtUtc },
        { "expiresAtUtc", entry.ExpiresAtUtc }
    };

    private static WriteError DuplicateKeyError() => (WriteError)Activator.CreateInstance(
        typeof(WriteError),
        BindingFlags.Instance | BindingFlags.NonPublic,
        null,
        [ServerErrorCategory.DuplicateKey, 11000, "duplicate", new BsonDocument()],
        null)!;
}
