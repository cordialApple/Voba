using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Repositories
{
    public class RecipeRepository : IRecipeRepository
    {
        private const string CollectionName = "recipes";
        private readonly IMongoCollection<Recipe> _collection;
        private readonly RepositoryIndexInitializer _indexes = new();

        public RecipeRepository(IMongoDatabase db)
        {
            _collection = db.GetCollection<Recipe>(CollectionName);
        }

        private Task EnsureIndexesAsync()
        {
            var userIdIndex = Builders<Recipe>.IndexKeys.Ascending(r => r.UserId);
            return _indexes.EnsureAsync(() => _collection.Indexes.CreateOneAsync(
                new CreateIndexModel<Recipe>(userIdIndex)));
        }

        public async Task<List<Recipe>> GetByUserIdAsync(string userId)
        {
            await EnsureIndexesAsync();
            var filter = Builders<Recipe>.Filter.Eq(r => r.UserId, userId);
            return await _collection.Find(filter).ToListAsync();
        }

        public async Task<Recipe> SaveAsync(Recipe recipe)
        {
            await EnsureIndexesAsync();
            await _collection.InsertOneAsync(recipe);
            return recipe;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            await EnsureIndexesAsync();
            var filter = Builders<Recipe>.Filter.Eq(r => r.Id, id);
            var result = await _collection.DeleteOneAsync(filter);
            return result.DeletedCount > 0;
        }
    }
}
