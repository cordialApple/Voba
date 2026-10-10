using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Repositories
{
    public class UserRepository : IUserRepository
    {
        private const string CollectionName = "users";
        private readonly IMongoCollection<User> _collection;
        private readonly RepositoryIndexInitializer _indexes = new();

        public UserRepository(IMongoDatabase db)
        {
            _collection = db.GetCollection<User>(CollectionName);
        }

        private Task EnsureIndexesAsync()
        {
            var emailIndex = Builders<User>.IndexKeys.Ascending(u => u.Email);
            return _indexes.EnsureAsync(() => _collection.Indexes.CreateOneAsync(new CreateIndexModel<User>(
                emailIndex,
                new CreateIndexOptions { Unique = true })));
        }

        public async Task<User?> GetByIdAsync(string id)
        {
            await EnsureIndexesAsync();
            var filter = Builders<User>.Filter.Eq(u => u.Id, id);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            await EnsureIndexesAsync();
            var filter = Builders<User>.Filter.Eq(u => u.Email, email.Trim().ToLowerInvariant());
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<User> SaveAsync(User user)
        {
            await EnsureIndexesAsync();
            await _collection.InsertOneAsync(user);
            return user;
        }

        public async Task<bool> UpdateAsync(User user)
        {
            await EnsureIndexesAsync();
            var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
            var result = await _collection.ReplaceOneAsync(filter, user);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            await EnsureIndexesAsync();
            var filter = Builders<User>.Filter.Eq(u => u.Id, id);
            var result = await _collection.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }
    }
}
