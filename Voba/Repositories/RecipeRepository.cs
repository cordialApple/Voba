using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Repositories
{
    public class RecipeRepository : IRecipeRepository
    {
        private const string CollectionName = "recipes";
        private readonly IMongoCollection<Recipe> _collection;

        public RecipeRepository(IMongoDatabase db)
        {
            _collection = db.GetCollection<Recipe>(CollectionName);
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            var userIdIndex = Builders<Recipe>.IndexKeys.Ascending(r => r.UserId);
            _collection.Indexes.CreateOne(new CreateIndexModel<Recipe>(userIdIndex));
        }

        public async Task<List<Recipe>> GetByUserIdAsync(string userId)
        {
            var filter = Builders<Recipe>.Filter.Eq(r => r.UserId, userId);
            return await _collection.Find(filter).ToListAsync();
        }

        public async Task<Recipe> SaveAsync(Recipe recipe)
        {
            await _collection.InsertOneAsync(recipe);
            return recipe;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var filter = Builders<Recipe>.Filter.Eq(r => r.Id, id);
            var result = await _collection.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }
    }
}
