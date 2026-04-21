using MongoDB.Driver;
using Voba.Models;
using Voba.Repositories;

var client = new MongoClient("your-atlas-connection-string");
var db = client.GetDatabase("voba");

// User test
var userRepo = new UserRepository(db);
var user = new User("test@test.com", "testuser");
await userRepo.SaveAsync(user);
var found = await userRepo.GetByEmailAsync("test@test.com");
Console.WriteLine(found is not null ? "User ✓" : "User X");

// Recipe test
var recipeRepo = new RecipeRepository(db);
var recipe = new Recipe(found!.Id, "Test Recipe",
    new() { new("chicken", 0m, "breast"), new("garlic", 2m, "cloves") },
    12.50m, "Step 1. Cook it.");
await recipeRepo.SaveAsync(recipe);
var recipes = await recipeRepo.GetByUserIdAsync(found.Id);
Console.WriteLine(recipes.Count > 0 ? "Recipe ✓" : "Recipe X");

// Ingredient catalog test
var ingredientRepo = new IngredientRepository(db);
// var entry = new IngredientCatalogEntry("chicken breast", 1234, 3.50m);
// await ingredientRepo.SaveAsync(entry);
var foundEntry = await ingredientRepo.GetByNameAsync("chicken breast");
Console.WriteLine(foundEntry is not null ? "Ingredient ✓" : "Ingredient X");