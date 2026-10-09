using MongoDB.Bson;
using MongoDB.Driver;
using Voba.Interfaces;
using Voba.Models;
using Voba.Services;

namespace Voba.Repositories;

public sealed class MongoUserRecipeStore : IUserRecipeStore
{
    private readonly IMongoCollection<Recipe> _collection;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private volatile bool _indexesReady;

    public MongoUserRecipeStore(IMongoDatabase database, string collectionName = "recipes")
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(collectionName);
        _collection = database.GetCollection<Recipe>(collectionName);
    }

    public async Task<Recipe> SaveAsync(string userId, RecipeGenerationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(context);
        if (!ObjectId.TryParse(userId, out _) ||
            string.IsNullOrWhiteSpace(context.FinalRecipe?.Instructions))
            throw new ArgumentException("A valid user and completed recipe are required.");
        await EnsureIndexesAsync(cancellationToken);
        var recipe = RecipeMapper.ToRecipe(context, userId);
        await _collection.InsertOneAsync(recipe, cancellationToken: cancellationToken);
        return recipe;
    }

    public async Task<List<Recipe>> ListAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        var filter = Builders<Recipe>.Filter.Eq(recipe => recipe.UserId, userId);
        return await _collection.Find(filter).SortByDescending(recipe => recipe.SavedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Recipe?> GetAsync(string recipeId, string userId,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(recipeId, out _) || !ObjectId.TryParse(userId, out _))
            return null;
        return await _collection.Find(OwnedFilter(recipeId, userId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string recipeId, string userId,
        CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(recipeId, out _) || !ObjectId.TryParse(userId, out _))
            return false;
        var result = await _collection.DeleteOneAsync(OwnedFilter(recipeId, userId), cancellationToken);
        return result.DeletedCount == 1;
    }

    private static FilterDefinition<Recipe> OwnedFilter(string recipeId, string userId) =>
        Builders<Recipe>.Filter.And(
            Builders<Recipe>.Filter.Eq(recipe => recipe.Id, recipeId),
            Builders<Recipe>.Filter.Eq(recipe => recipe.UserId, userId));

    private async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        if (_indexesReady)
            return;
        await _indexGate.WaitAsync(cancellationToken);
        try
        {
            if (_indexesReady)
                return;
            await _collection.Indexes.CreateOneAsync(new CreateIndexModel<Recipe>(
                Builders<Recipe>.IndexKeys.Ascending(recipe => recipe.UserId)),
                cancellationToken: cancellationToken);
            _indexesReady = true;
        }
        finally
        {
            _indexGate.Release();
        }
    }
}
