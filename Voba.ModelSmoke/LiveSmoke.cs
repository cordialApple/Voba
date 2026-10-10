using Microsoft.SemanticKernel;
using MongoDB.Driver;
using Voba.AI.Pipeline.Handlers;
using Voba.Models;
using Voba.Repositories;
using Voba.Services;

internal static class LiveSmoke
{
    public static async Task RunAsync(Kernel kernel, string mongoUri, string model)
    {
        var settings = MongoClientSettings.FromConnectionString(mongoUri);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(15);
        var database = new MongoClient(settings).GetDatabase(
            Environment.GetEnvironmentVariable("VOBA_TEST_MONGO_DATABASE") ?? "VobaDemoTests");
        var id = Guid.NewGuid().ToString("N");
        var email = $"voba-smoke-{id}@example.invalid";
        var password = Guid.NewGuid().ToString("N");
        var cacheCollection = $"recipe_cache_smoke_{id}";
        try
        {
            var users = new UserRepository(database);
            var authData = new AuthDataRepository(database);
            var recipes = new RecipeRepository(database);
            var jwt = new JwtService();
            var auth = new AuthService(users, authData, new BcryptPasswordHasher(), jwt);
            var registration = await auth.RegisterAsync(email, "Voba Smoke", password);
            Require(registration.Success && registration.Data is not null, "Sign-up failed.");
            var userId = registration.Data!.Id;
            Require(!string.IsNullOrWhiteSpace(userId), "Sign-up returned no user ID.");

            var login = await auth.LoginAsync(email, password);
            Require(login.Success && login.Data?.UserId == userId, "Login failed.");
            Require(jwt.ValidateToken(login.Data!.AccessToken), "Login token invalid.");
            var session = new CurrentUserService();
            session.SetUser(userId, email);
            Require(session.IsAuthenticated, "Session not set.");

            var cache = new MongoRecipeGenerationCache(database, cacheCollection);
            var coordinator = new RecipeGenerationCoordinator(cache, TimeProvider.System,
                TimeSpan.FromHours(1), model, RecipeGenerationCacheVersion.Current);
            var ideation = new GemmaIdeationHandler(kernel);
            ideation.SetNext(new SpoonacularPricingHandler(new FakeEnrichmentService()));
            var optionCalls = 0;
            var options = await coordinator.GetOptionsAsync(Request(), RecipeDataSource.Synthetic,
                async context =>
                {
                    optionCalls++;
                    await ideation.HandleAsync(context);
                    context.ProposedOptions = RecipeGenerationPolicy.WithinBudget(
                        context.ProposedOptions, context.TargetBudget, context.ServingSize);
                });
            Require(options.ProposedOptions.Count > 0, "Gemma returned no usable options.");
            Require(options.ProposedOptions.All(option => option.DataSource == RecipeDataSource.Synthetic),
                "Option source mismatch.");
            Require(options.ProposedOptions.All(option => DietaryCompliancePolicy.AllowsOption(
                    option, options.DietaryRestrictions)), "Dietary restriction violated by option.");
            var optionsHit = await coordinator.GetOptionsAsync(Request(), RecipeDataSource.Synthetic,
                _ => throw new InvalidOperationException("Options cache miss."));
            Require(optionCalls == 1 && optionsHit.ProposedOptions.Count > 0,
                "Options cache did not skip generation.");

            options.SelectedOption = options.ProposedOptions[0];
            var selected = options.SelectedOption;
            var fullCalls = 0;
            var fullHandler = new GemmaFullRecipeHandler(kernel);
            var full = await coordinator.GetFullRecipeAsync(options, RecipeDataSource.Synthetic,
                async context =>
                {
                    fullCalls++;
                    await fullHandler.HandleAsync(context);
                });
            Require(!string.IsNullOrWhiteSpace(full.FinalRecipe?.Instructions),
                "Gemma returned no cooking instructions.");
            Require(DietaryCompliancePolicy.AllowsFullRecipe(full.SelectedOption!,
                    full.FinalRecipe!.Instructions, full.DietaryRestrictions),
                "Dietary restriction violated by instructions.");
            var fullHit = await coordinator.GetFullRecipeAsync(options, RecipeDataSource.Synthetic,
                _ => throw new InvalidOperationException("Full recipe cache miss."));
            Require(fullCalls == 1 && fullHit.FinalRecipe?.Instructions == full.FinalRecipe!.Instructions,
                "Full recipe cache did not skip generation.");
            Require(selected.DataSource == RecipeDataSource.Synthetic,
                "Selected source changed unexpectedly.");

            var saved = await recipes.SaveAsync(RecipeMapper.ToRecipe(full, userId));
            var loaded = await recipes.GetByUserIdAsync(userId);
            Require(loaded.Count == 1 && loaded[0].Id == saved.Id,
                "Saved recipe did not reload.");
            Require(loaded[0].DataSource == RecipeDataSource.Synthetic &&
                    loaded[0].NutritionSource == RecipeDataSource.Synthetic,
                "Saved source metadata changed.");

            var now = DateTime.UtcNow;
            var authorityKey = $"authority_{id}";
            var real = new RecipeGenerationCacheEntry(authorityKey, RecipeDataSource.Real,
                "real", now, now.AddMinutes(5));
            var synthetic = new RecipeGenerationCacheEntry(authorityKey, RecipeDataSource.Synthetic,
                "synthetic", now, now.AddMinutes(5));
            await cache.StoreAsync(real);
            Require((await cache.StoreAsync(synthetic)).Source == RecipeDataSource.Real,
                "Synthetic entry demoted real entry.");
            Require((await cache.GetAsync(authorityKey))?.PayloadJson == "real",
                "Real entry lost authority.");

            Require(await auth.LogoutAsync(userId), "Logout failed.");
            session.Clear();
            Require(!session.IsAuthenticated, "Session not cleared.");
            Console.WriteLine("Live smoke passed: signup, login, options cache, full recipe cache, save/reload, source precedence, logout.");
        }
        finally
        {
            var userCollection = database.GetCollection<User>("users");
            var ownedUser = await userCollection.Find(user => user.Email == email).FirstOrDefaultAsync();
            if (ownedUser is not null)
            {
                await database.GetCollection<Recipe>("recipes")
                    .DeleteManyAsync(recipe => recipe.UserId == ownedUser.Id);
                await database.GetCollection<AuthData>("authdata")
                    .DeleteManyAsync(data => data.UserId == ownedUser.Id);
                await userCollection.DeleteOneAsync(user => user.Id == ownedUser.Id && user.Email == email);
            }
            await database.DropCollectionAsync(cacheCollection);
        }
    }

    private static RecipeGenerationContext Request() => new()
    {
        ServingSize = 2,
        TargetBudget = 100m,
        DietaryRestrictions = ["vegan"]
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
