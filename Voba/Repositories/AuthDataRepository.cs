using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Repositories
{
    public class AuthDataRepository : IAuthDataRepository
    {
        private const string CollectionName = "authdata";
        private readonly IMongoCollection<AuthData> _collection;

        public AuthDataRepository(IMongoDatabase db)
        {
            _collection = db.GetCollection<AuthData>(CollectionName);
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            var userIndex = Builders<AuthData>.IndexKeys.Ascending(ad => ad.UserId);
            _collection.Indexes.CreateOne(new CreateIndexModel<AuthData>(userIndex));

            var refreshTokenIndex = Builders<AuthData>.IndexKeys.Ascending(ad => ad.RefreshToken);
            _collection.Indexes.CreateOne(new CreateIndexModel<AuthData>(refreshTokenIndex));
        }

        /// <summary>Returns the auth data for the given user, or null if not found.</summary>
        public async Task<AuthData?> GetByUserIdAsync(string userId)
        {
            var filter = Builders<AuthData>.Filter.Eq(ad => ad.UserId, userId);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        /// <summary>Inserts a new auth data document and returns the saved record.</summary>
        public async Task<AuthData> SaveAsync(AuthData data)
        {
            await _collection.InsertOneAsync(data);
            return data;
        }

        /// <summary>Replaces the auth data document matched by Id. Returns true if a document was updated.</summary>
        public async Task<bool> UpdateAsync(AuthData data)
        {
            var filter = Builders<AuthData>.Filter.Eq(ad => ad.Id, data.Id);
            var result = await _collection.ReplaceOneAsync(filter, data);
            return result.ModifiedCount > 0;
        }

        /// <summary>Returns the auth data matching the refresh token, or null if not found.</summary>
        public async Task<AuthData?> GetByRefreshTokenAsync(string refreshToken)
        {
            var filter = Builders<AuthData>.Filter.Eq(ad => ad.RefreshToken, refreshToken);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }
    }
}
