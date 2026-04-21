using MongoDB.Driver;
using Voba.Interfaces;

namespace Voba.Repositories
{
    public static class RepositoryFactory
    {
        public static IUserRepository CreateUserRepository(IMongoDatabase db) =>
            new UserRepository(db);

        public static IGroceryListRepository CreateGroceryListRepository(IMongoDatabase db) =>
            new GroceryListRepository(db);

        public static IAuthDataRepository CreateAuthDataRepository(IMongoDatabase db) =>
            new AuthDataRepository(db);

        public static IRecipeRepository CreateRecipeRepository(IMongoDatabase db) =>
            new RecipeRepository(db);

        public static IIngredientRepository CreateIngredientRepository(IMongoDatabase db) =>
            new IngredientRepository(db);
    }
}
