using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Repositories;

public sealed class MongoGenerationDraftStore : IGenerationDraftStore
{
    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private volatile bool _indexesReady;

    public MongoGenerationDraftStore(IMongoDatabase database,
        string collectionName = "generation_drafts")
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        _collection = database.GetCollection<BsonDocument>(collectionName);
    }

    public async Task<GenerationDraft> CreateAsync(string userId, RecipeGenerationContext context,
        DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(context);
        await EnsureIndexesAsync(cancellationToken);
        var draft = new GenerationDraft(Guid.NewGuid().ToString("N"), userId,
            context, expiresAtUtc, 0);
        await _collection.InsertOneAsync(new BsonDocument
        {
            { "_id", draft.Id },
            { "userId", draft.UserId },
            { "contextJson", JsonSerializer.Serialize(context) },
            { "expiresAtUtc", draft.ExpiresAtUtc },
            { "version", draft.Version }
        }, cancellationToken: cancellationToken);
        return draft;
    }

    public async Task<GenerationDraft?> GetAsync(string id, string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var document = await _collection.Find(OwnedLiveFilter(id, userId))
            .FirstOrDefaultAsync(cancellationToken);
        return document is null ? null : ToDraft(document);
    }

    public async Task<bool> ReplaceAsync(GenerationDraft original, RecipeGenerationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(context);
        var filter = Builders<BsonDocument>.Filter.And(
            OwnedLiveFilter(original.Id, original.UserId),
            Builders<BsonDocument>.Filter.Eq("version", original.Version));
        var update = Builders<BsonDocument>.Update
            .Set("contextJson", JsonSerializer.Serialize(context))
            .Inc("version", 1);
        var result = await _collection.UpdateOneAsync(filter, update,
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    private static FilterDefinition<BsonDocument> OwnedLiveFilter(string id, string userId) =>
        Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Filter.Eq("userId", userId),
            Builders<BsonDocument>.Filter.Gt("expiresAtUtc", DateTime.UtcNow));

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_indexesReady)
            return;
        await _indexGate.WaitAsync(cancellationToken);
        try
        {
            if (_indexesReady)
                return;
            await _collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("expiresAtUtc"),
                new CreateIndexOptions { ExpireAfter = TimeSpan.Zero }),
                cancellationToken: cancellationToken);
            _indexesReady = true;
        }
        finally
        {
            _indexGate.Release();
        }
    }

    private static GenerationDraft ToDraft(BsonDocument document) => new(
        document["_id"].AsString,
        document["userId"].AsString,
        JsonSerializer.Deserialize<RecipeGenerationContext>(document["contextJson"].AsString)!,
        document["expiresAtUtc"].ToUniversalTime(),
        document["version"].ToInt64());
}
