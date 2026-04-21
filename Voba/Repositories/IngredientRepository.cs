using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Repositories
{
    public class IngredientRepository : IIngredientRepository
    {
        private const string CollectionName = "ingredients";
        private readonly IMongoCollection<Ingredient> _collection;

        public IngredientRepository(IMongoDatabase db)
        {
            _collection = db.GetCollection<Ingredient>(CollectionName);
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            var nameIndex = Builders<Ingredient>.IndexKeys.Ascending(i => i.Name);
            _collection.Indexes.CreateOne(new CreateIndexModel<Ingredient>(
                nameIndex,
                new CreateIndexOptions { Unique = true }));
        }

        public async Task<Ingredient?> GetByNameAsync(string name)
        {
            var filter = Builders<Ingredient>.Filter.Eq(i => i.Name, name.Trim());
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<Ingredient> SaveAsync(Ingredient ingredient)
        {
            await _collection.InsertOneAsync(ingredient);
            return ingredient;
        }

        public async Task<bool> UpdateAsync(Ingredient ingredient)
        {
            var filter = Builders<Ingredient>.Filter.Eq(i => i.Id, ingredient.Id);
            var result = await _collection.ReplaceOneAsync(filter, ingredient);
            return result.ModifiedCount > 0;
        }
    }
}
