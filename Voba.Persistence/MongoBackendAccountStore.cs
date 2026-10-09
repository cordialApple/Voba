using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Repositories;

public sealed class MongoBackendAccountStore : IBackendAccountStore
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<AuthData> _authData;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private volatile bool _indexesReady;

    public MongoBackendAccountStore(IMongoDatabase database, string usersCollection = "users",
        string authDataCollection = "authdata")
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(usersCollection);
        ArgumentException.ThrowIfNullOrWhiteSpace(authDataCollection);
        _database = database;
        _users = database.GetCollection<User>(usersCollection);
        _authData = database.GetCollection<AuthData>(authDataCollection);
    }

    public async Task CreateAsync(User user, string password, IPasswordHasher hasher,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(hasher);
        await EnsureIndexesAsync(cancellationToken);
        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        await session.WithTransactionAsync(async (transaction, ct) =>
        {
            await _users.InsertOneAsync(transaction, user, cancellationToken: ct);
            var credentials = new AuthData(user.Id);
            credentials.SetPassword(password, hasher);
            await _authData.InsertOneAsync(transaction, credentials, cancellationToken: ct);
            return true;
        }, new TransactionOptions(writeConcern: WriteConcern.WMajority), cancellationToken);
    }

    public async Task<User?> GetUserByEmailAsync(string email,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        var filter = Builders<User>.Filter.Eq(user => user.Email, email.Trim().ToLowerInvariant());
        return await _users.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<User?> GetUserByIdAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var filter = Builders<User>.Filter.Eq(user => user.Id, userId);
        return await _users.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AuthData?> GetAuthDataByUserIdAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var filter = Builders<AuthData>.Filter.Eq(data => data.UserId, userId);
        return await _authData.Find(filter).FirstOrDefaultAsync(cancellationToken);
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
            await _users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(user => user.Email),
                new CreateIndexOptions { Unique = true }), cancellationToken: cancellationToken);
            _indexesReady = true;
        }
        finally
        {
            _indexGate.Release();
        }
    }
}
