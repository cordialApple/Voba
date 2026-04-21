using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Repositories
{
    public class GroceryListRepository : IGroceryListRepository
    {
        private const string CollectionName = "grocerylists";
        private readonly IMongoCollection<GroceryList> _collection;

        public GroceryListRepository(IMongoDatabase db)
        {
            _collection = db.GetCollection<GroceryList>(CollectionName);
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            var userIndex = Builders<GroceryList>.IndexKeys.Ascending(gl => gl.UserId);
            _collection.Indexes.CreateOne(new CreateIndexModel<GroceryList>(userIndex));

            var compoundIndex = Builders<GroceryList>.IndexKeys
                .Ascending(gl => gl.UserId)
                .Ascending(gl => gl.CreatedAt);
            _collection.Indexes.CreateOne(new CreateIndexModel<GroceryList>(compoundIndex));
        }

        public async Task<GroceryList?> GetByIdAsync(string id)
        {
            var filter = Builders<GroceryList>.Filter.Eq(gl => gl.Id, id);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<List<GroceryList>> GetByUserIdAsync(string userId)
        {
            var filter = Builders<GroceryList>.Filter.Eq(gl => gl.UserId, userId);
            return await _collection.Find(filter).ToListAsync();
        }

        public async Task<GroceryList> SaveAsync(GroceryList list)
        {
            await _collection.InsertOneAsync(list);
            return list;
        }

        public async Task<bool> UpdateAsync(GroceryList list)
        {
            var filter = Builders<GroceryList>.Filter.Eq(gl => gl.Id, list.Id);
            var result = await _collection.ReplaceOneAsync(filter, list);
            return result.ModifiedCount > 0;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var filter = Builders<GroceryList>.Filter.Eq(gl => gl.Id, id);
            var result = await _collection.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }
    }
}
