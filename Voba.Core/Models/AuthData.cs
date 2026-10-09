using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Voba.Interfaces;

namespace Voba.Models
{
    public class AuthData
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; private set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string UserId { get; private set; }

        public string PasswordHash { get; private set; } = string.Empty;
        public string Salt { get; private set; } = string.Empty;
        public string? RefreshToken { get; private set; }
        public DateTime? TokenExpiry { get; private set; }

        public AuthData(string userId)
        {
            UserId = userId;
        }

        public void SetPassword(string plainText, IPasswordHasher hasher)
        {
            Salt         = hasher.GenerateSalt();
            PasswordHash = hasher.Hash(plainText, Salt);
        }

        public bool VerifyPassword(string plainText, IPasswordHasher hasher) =>
            hasher.Verify(plainText, PasswordHash);

        public void SetRefreshToken(string token, DateTime expiry)
        {
            RefreshToken = token;
            TokenExpiry  = expiry;
        }

        public void ClearRefreshToken()
        {
            RefreshToken = null;
            TokenExpiry  = null;
        }
    }
}
