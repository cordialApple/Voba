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

        public DateTime SavedAt { get; private set; }

        public Recipe(string userId, string title, List<Ingredient> ingredients,
            decimal estimatedCost, string instructions)
        {
            UserId        = userId;
            Title         = title;
            Ingredients   = ingredients;
            EstimatedCost = estimatedCost;
            Instructions  = instructions;
            SavedAt       = DateTime.UtcNow;
        }
    }
}
