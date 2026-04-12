using MongoDB.Driver;
using Voba.Interfaces;

namespace Voba.Repositories
{
    /// <summary>
    /// Centralises repository creation so a Go-port swap touches only this file.
    /// </summary>
    public static class RepositoryFactory
    {
        /// <summary>Creates a UserRepository backed by the given database.</summary>
        public static IUserRepository CreateUserRepository(IMongoDatabase db) =>
            new UserRepository(db);

        /// <summary>Creates a GroceryListRepository backed by the given database.</summary>
        public static IGroceryListRepository CreateGroceryListRepository(IMongoDatabase db) =>
            new GroceryListRepository(db);

        /// <summary>Creates an AuthDataRepository backed by the given database.</summary>
        public static IAuthDataRepository CreateAuthDataRepository(IMongoDatabase db) =>
            new AuthDataRepository(db);
    }
}
