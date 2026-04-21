using Voba.Interfaces;

namespace Voba.api
{
    /// <summary>
    /// Offline Spoonacular stub — returns hardcoded recipes and costs without any network call or API key.
    /// Swap registration to SpoonacularService when running with a live key (Vathana swaps on merge).
    /// </summary>
    public class FakeSpoonacularService : ISpoonacularService
    {
        private static readonly List<SpoonacularSearchResult> _allResults = new()
        {
            new(1001, "Chicken Stir-Fry with Vegetables", "https://spoonacular.com/recipeImages/1001.jpg"),
            new(1002, "Spaghetti Bolognese",               "https://spoonacular.com/recipeImages/1002.jpg"),
            new(1003, "Vegetable Curry",                    "https://spoonacular.com/recipeImages/1003.jpg"),
            new(1004, "Beef Tacos",                         "https://spoonacular.com/recipeImages/1004.jpg"),
            new(1005, "Caesar Salad",                       "https://spoonacular.com/recipeImages/1005.jpg"),
        };

        private static readonly Dictionary<int, SpoonacularRecipeDetail> _details = new()
        {
            [1001] = new SpoonacularRecipeDetail(1001, "Chicken Stir-Fry with Vegetables",
                new List<SpoonacularIngredient>
                {
                    new("chicken breast", 300m, "g",      2.50m),
                    new("broccoli",       200m, "g",      1.20m),
                    new("bell pepper",      1m, "whole",  0.90m),
                    new("soy sauce",       30m, "ml",     0.40m),
                    new("garlic",           3m, "cloves", 0.30m),
                }, 5.30m),

            [1002] = new SpoonacularRecipeDetail(1002, "Spaghetti Bolognese",
                new List<SpoonacularIngredient>
                {
                    new("ground beef",   400m, "g",     3.50m),
                    new("spaghetti",     250m, "g",     1.10m),
                    new("tomato sauce",  400m, "ml",    1.20m),
                    new("onion",           1m, "whole", 0.40m),
                    new("parmesan",       50m, "g",     1.80m),
                }, 8.00m),

            [1003] = new SpoonacularRecipeDetail(1003, "Vegetable Curry",
                new List<SpoonacularIngredient>
                {
                    new("chickpeas",    400m, "g",     1.20m),
                    new("tomatoes",     300m, "g",     0.90m),
                    new("coconut milk", 400m, "ml",    1.80m),
                    new("curry powder",  15m, "g",     0.60m),
                    new("onion",          1m, "whole", 0.40m),
                }, 4.90m),

            [1004] = new SpoonacularRecipeDetail(1004, "Beef Tacos",
                new List<SpoonacularIngredient>
                {
                    new("ground beef",    300m, "g",      3.50m),
                    new("taco shells",      8m, "shells", 1.40m),
                    new("cheddar cheese",  80m, "g",      1.50m),
                    new("salsa",          100m, "ml",     0.80m),
                    new("sour cream",      60m, "ml",     0.70m),
                }, 7.90m),

            [1005] = new SpoonacularRecipeDetail(1005, "Caesar Salad",
                new List<SpoonacularIngredient>
                {
                    new("romaine lettuce",  1m, "head",  1.50m),
                    new("parmesan",        40m, "g",     1.80m),
                    new("caesar dressing", 60m, "ml",    1.40m),
                    new("croutons",        50m, "g",     0.90m),
                    new("lemon",            1m, "whole", 0.50m),
                }, 6.10m),
        };

        /// <summary>Returns a hardcoded list of up to <paramref name="number"/> recipes; query is ignored.</summary>
        public Task<List<SpoonacularSearchResult>?> SearchRecipesAsync(string query, int number = 5)
        {
            var results = _allResults.Take(number).ToList();
            return Task.FromResult<List<SpoonacularSearchResult>?>(results);
        }

        /// <summary>Returns hardcoded recipe detail for the given ID, or null if the ID is not in the stub data.</summary>
        public Task<SpoonacularRecipeDetail?> GetRecipeByIdAsync(int recipeId)
        {
            _details.TryGetValue(recipeId, out var detail);
            return Task.FromResult(detail);
        }
    }
}
