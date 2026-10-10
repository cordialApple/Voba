using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Voba.Models
{
    public class Recipe
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; private set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; private set; } = string.Empty;

        public string Title { get; private set; } = string.Empty;

        public List<Ingredient> Ingredients { get; private set; } = new();

        public decimal EstimatedCost { get; private set; }

        public string Instructions { get; private set; } = string.Empty;

        public NutritionInfo? Nutrition { get; private set; }

        public DateTime SavedAt { get; private set; }

        public RecipeDataSource DataSource { get; private set; }

        public RecipeDataSource NutritionSource { get; private set; }

        public int? Servings { get; private set; }

        public decimal? Budget { get; private set; }

        public List<string>? DietaryRestrictions { get; private set; }

        public string? CuisinePreference { get; private set; }

        [BsonConstructor]
        private Recipe()
        {
        }

        public Recipe(string userId, string title, List<Ingredient> ingredients,
            decimal estimatedCost, string instructions, NutritionInfo? nutrition = null,
            RecipeDataSource dataSource = RecipeDataSource.Estimate,
            RecipeDataSource nutritionSource = RecipeDataSource.Estimate,
            int? servings = null, decimal? budget = null,
            List<string>? dietaryRestrictions = null, string? cuisinePreference = null)
        {
            UserId        = userId;
            Title         = title;
            Ingredients   = ingredients;
            EstimatedCost = estimatedCost;
            Instructions  = instructions;
            Nutrition     = nutrition;
            DataSource    = dataSource;
            NutritionSource = nutritionSource;
            Servings = servings;
            Budget = budget;
            DietaryRestrictions = dietaryRestrictions?.ToList();
            CuisinePreference = cuisinePreference;
            SavedAt       = DateTime.UtcNow;
        }
    }
}
