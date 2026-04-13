using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Voba.Models
{
    public class GroceryList
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; private set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; private set; }

        public List<Ingredient> Items { get; private set; }
        public decimal Budget { get; private set; }
        public decimal TotalEstimatedCost { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        internal GroceryList(string userId, decimal budget, List<Ingredient> items, decimal estimatedCost)
        {
            UserId             = userId;
            Budget             = budget;
            Items              = items;
            TotalEstimatedCost = estimatedCost;
            CreatedAt          = DateTime.UtcNow;
            UpdatedAt          = DateTime.UtcNow;
        }
    }
}
