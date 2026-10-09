using MongoDB.Bson;
using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Repositories;

public sealed class MongoRecipeGenerationCache : IRecipeGenerationCache
{
    private const string CollectionName = "recipe_generation_cache";
    private readonly Func<FilterDefinition<BsonDocument>, UpdateDefinition<BsonDocument>, bool, CancellationToken, Task> _update;
    private readonly Func<FilterDefinition<BsonDocument>, CancellationToken, Task<BsonDocument?>> _read;

    public MongoRecipeGenerationCache(IMongoDatabase database) : this(database, CollectionName)
    {
    }

    public MongoRecipeGenerationCache(IMongoDatabase database, string collectionName) : this(CreateCollection(database, collectionName))
    {
    }

    private MongoRecipeGenerationCache(IMongoCollection<BsonDocument> collection) : this(
        async (filter, update, upsert, cancellationToken) =>
            await collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = upsert }, cancellationToken),
        async (filter, cancellationToken) =>
            await collection.Find(filter).FirstOrDefaultAsync(cancellationToken))
    {
    }

    internal MongoRecipeGenerationCache(
        Func<FilterDefinition<BsonDocument>, UpdateDefinition<BsonDocument>, bool, CancellationToken, Task> update,
        Func<FilterDefinition<BsonDocument>, CancellationToken, Task<BsonDocument?>> read)
    {
        _update = update;
        _read = read;
    }

    public async Task<RecipeGenerationCacheEntry?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var document = await _read(CreateReadFilter(key, DateTime.UtcNow), cancellationToken);
        return document is null ? null : ToEntry(document);
    }

    public async Task<RecipeGenerationCacheEntry> StoreAsync(RecipeGenerationCacheEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentException.ThrowIfNullOrWhiteSpace(entry.Key);
        ArgumentNullException.ThrowIfNull(entry.PayloadJson);
        cancellationToken.ThrowIfCancellationRequested();

        var filter = CreateStoreFilter(entry.Key, entry.Source);
        var update = Builders<BsonDocument>.Update
            .Set("source", (int)entry.Source)
            .Set("payloadJson", entry.PayloadJson)
            .Set("createdAtUtc", entry.CreatedAtUtc)
            .Set("expiresAtUtc", entry.ExpiresAtUtc);

        try
        {
            await _update(filter, update, true, cancellationToken);
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            await _update(filter, update, false, cancellationToken);
        }

        var winner = await _read(Builders<BsonDocument>.Filter.Eq("_id", entry.Key), cancellationToken);
        return winner is null ? throw new InvalidOperationException("Cache entry disappeared after write.") : ToEntry(winner);
    }

    internal static FilterDefinition<BsonDocument> CreateReadFilter(string key, DateTime nowUtc) =>
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", key),
            Builders<BsonDocument>.Filter.Gt("expiresAtUtc", nowUtc));

    internal static FilterDefinition<BsonDocument> CreateStoreFilter(string key, RecipeDataSource source) =>
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", key),
            Builders<BsonDocument>.Filter.Lte("source", (int)source));

    private static IMongoCollection<BsonDocument> CreateCollection(IMongoDatabase database, string collectionName)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        return database.GetCollection<BsonDocument>(collectionName);
    }

    private static RecipeGenerationCacheEntry ToEntry(BsonDocument document) => new(
        document["_id"].AsString,
        (RecipeDataSource)document["source"].AsInt32,
        document["payloadJson"].AsString,
        document["createdAtUtc"].ToUniversalTime(),
        document["expiresAtUtc"].ToUniversalTime());
}
