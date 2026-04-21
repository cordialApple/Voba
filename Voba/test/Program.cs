using MongoDB.Driver;
using Voba;
using Voba.Models;
using Voba.Repositories;

var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

var client = new MongoClient(Secrets.MongoConnectionString);
var db = client.GetDatabase(Secrets.MongoDatabaseName);

// ── users collection ──────────────────────────────────────────────────────────
var userRepo = new UserRepository(db);
var email = $"test-{ts}@test.com";
var user = new User(email, $"testuser-{ts}");
await userRepo.SaveAsync(user);
var found = await userRepo.GetByEmailAsync(email);
Console.WriteLine(found is not null ? "User ✓" : "User ✗");

// ── recipes collection ────────────────────────────────────────────────────────
var recipeRepo = new RecipeRepository(db);
var recipe = new Recipe(
    found!.Id,
    "Test Recipe",
    new List<Ingredient>(),   // empty list — Ingredient.Id defaults to string.Empty, which cannot
    12.50m,                   // be serialized as BsonObjectId in an embedded sub-document
    "Step 1. Cook it.");
await recipeRepo.SaveAsync(recipe);
var recipes = await recipeRepo.GetByUserIdAsync(found.Id);
Console.WriteLine(recipes.Count > 0 ? "Recipe ✓" : "Recipe ✗");

// ── ingredients collection ────────────────────────────────────────────────────
var ingredientRepo = new IngredientRepository(db);
var ingredientName = $"chicken-breast-{ts}";
var entry = new Ingredient(ingredientName, 1m, "ea");
await ingredientRepo.SaveAsync(entry);
var foundEntry = await ingredientRepo.GetByNameAsync(ingredientName);
Console.WriteLine(foundEntry is not null ? "Ingredient ✓" : "Ingredient ✗");

Console.WriteLine($"\nDatabase: {Secrets.MongoDatabaseName}");
Console.WriteLine($"Collections written: users, recipes, ingredients");
