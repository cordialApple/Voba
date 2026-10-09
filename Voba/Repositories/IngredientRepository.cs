using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Repositories
{
    public class IngredientRepository : IIngredientRepository
    {
        private const string CollectionName = "ingredients";
        private readonly IMongoCollection<Ingredient> _collection;
        private readonly RepositoryIndexInitializer _indexes = new();

        public IngredientRepository(IMongoDatabase db)
        {
            _collection = db.GetCollection<Ingredient>(CollectionName);
        }

        private Task EnsureIndexesAsync()
        {
            var nameIndex = Builders<Ingredient>.IndexKeys.Ascending(i => i.Name);
            return _indexes.EnsureAsync(() => _collection.Indexes.CreateOneAsync(new CreateIndexModel<Ingredient>(
                nameIndex,
                new CreateIndexOptions { Unique = true })));
        }

        public async Task<Ingredient?> GetByNameAsync(string name)
        {
            await EnsureIndexesAsync();
            var filter = Builders<Ingredient>.Filter.Eq(i => i.Name, name.Trim());
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<Ingredient> SaveAsync(Ingredient ingredient)
        {
            await EnsureIndexesAsync();
            await _collection.InsertOneAsync(ingredient);
            return ingredient;
        }

        public async Task<bool> UpdateAsync(Ingredient ingredient)
        {
            await EnsureIndexesAsync();
            var filter = Builders<Ingredient>.Filter.Eq(i => i.Id, ingredient.Id);
            var result = await _collection.ReplaceOneAsync(filter, ingredient);
            return result.ModifiedCount > 0;
        }
    }
}
