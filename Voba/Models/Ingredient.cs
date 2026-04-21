using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Voba.Models
{
    public class Ingredient
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; private set; } = string.Empty;

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Name cannot be empty.");
                _name = value.Trim();
            }
        }

        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal EstimatedCost { get; set; }
        public int SpoonacularId { get; set; }
        public DateTime LastUpdated { get; set; }

        public Ingredient(string name, decimal quantity, string unit)
        {
            Name = name;
            Quantity = quantity;
            Unit = unit;
        }
    }
}
