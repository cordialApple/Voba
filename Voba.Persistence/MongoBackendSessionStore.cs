using MongoDB.Bson;
using MongoDB.Driver;

namespace Voba.Repositories;

public sealed class MongoBackendSessionStore : IBackendSessionStore
{
    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private volatile bool _indexesReady;

    public MongoBackendSessionStore(IMongoDatabase database, string collectionName = "sessions")
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        _collection = database.GetCollection<BsonDocument>(collectionName);
    }

    public async Task CreateAsync(BackendSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await EnsureIndexesAsync(cancellationToken);
        await _collection.InsertOneAsync(new BsonDocument
        {
            { "_id", session.Id },
            { "userId", session.UserId },
            { "refreshHash", session.RefreshHash },
            { "expiresAtUtc", session.ExpiresAtUtc },
            { "revokedAtUtc", BsonNull.Value }
        }, cancellationToken: cancellationToken);
    }

    public async Task<BackendSession?> RotateAsync(string oldRefreshHash, string newRefreshHash,
        DateTime nowUtc, DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oldRefreshHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(newRefreshHash);
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("refreshHash", oldRefreshHash),
            Builders<BsonDocument>.Filter.Eq("revokedAtUtc", BsonNull.Value),
            Builders<BsonDocument>.Filter.Gt("expiresAtUtc", nowUtc));
        var update = Builders<BsonDocument>.Update
            .Set("refreshHash", newRefreshHash)
            .Set("expiresAtUtc", expiresAtUtc);
        var document = await _collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return document is null ? null : ToSession(document);
    }

    public async Task<bool> IsActiveAsync(string sessionId, string userId, DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", sessionId),
            Builders<BsonDocument>.Filter.Eq("userId", userId),
            Builders<BsonDocument>.Filter.Eq("revokedAtUtc", BsonNull.Value),
            Builders<BsonDocument>.Filter.Gt("expiresAtUtc", nowUtc));
        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<bool> RevokeAsync(string sessionId, string userId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", sessionId),
            Builders<BsonDocument>.Filter.Eq("userId", userId),
            Builders<BsonDocument>.Filter.Eq("revokedAtUtc", BsonNull.Value));
        var result = await _collection.UpdateOneAsync(filter,
            Builders<BsonDocument>.Update.Set("revokedAtUtc", DateTime.UtcNow),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

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
                Builders<BsonDocument>.IndexKeys.Ascending("refreshHash"),
                new CreateIndexOptions { Unique = true }), cancellationToken: cancellationToken);
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

    private static BackendSession ToSession(BsonDocument document) => new(
        document["_id"].AsString,
        document["userId"].AsString,
        document["refreshHash"].AsString,
        document["expiresAtUtc"].ToUniversalTime(),
        document["revokedAtUtc"].IsBsonNull ? null : document["revokedAtUtc"].ToUniversalTime());
}
