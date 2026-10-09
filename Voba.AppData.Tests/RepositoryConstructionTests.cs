using MongoDB.Driver;
using Voba.Repositories;
using Xunit;

namespace Voba.AppData.Tests;

public class RepositoryConstructionTests
{
    [Fact]
    public void Constructors_do_not_connect_to_database()
    {
        var client = new MongoClient("mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=100");
        var database = client.GetDatabase("offline-test");

        Assert.NotNull(new UserRepository(database));
        Assert.NotNull(new AuthDataRepository(database));
        Assert.NotNull(new RecipeRepository(database));
        Assert.NotNull(new IngredientRepository(database));
    }
}
