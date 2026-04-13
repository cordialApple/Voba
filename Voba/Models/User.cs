using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Voba.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; private set; } = string.Empty;

        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Email cannot be empty.");
                if (!value.Contains('@'))
                    throw new ArgumentException("Email must contain '@'.");
                _email = value.Trim().ToLowerInvariant();
            }
        }

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("Username cannot be empty.");
                _username = value.Trim();
            }
        }

        private decimal _budget;
        public decimal Budget
        {
            get => _budget;
            set
            {
                if (value < 0)
                    throw new ArgumentException("Budget cannot be negative.");
                _budget = value;
            }
        }

        public DateTime CreatedAt { get; private set; }

        public User(string email, string username, decimal budget)
        {
            Email = email;
            Username = username;
            Budget = budget;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
