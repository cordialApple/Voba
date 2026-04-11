using Voba.Interfaces;

namespace Voba.Services
{
    public class BcryptPasswordHasher : IPasswordHasher
    {
        /// <summary>Generates a BCrypt salt.</summary>
        public string GenerateSalt() =>
            BCrypt.Net.BCrypt.GenerateSalt();

        /// <summary>Hashes plaintext with BCrypt.</summary>
        public string Hash(string plainText, string salt) =>
            BCrypt.Net.BCrypt.HashPassword(plainText, salt);

        /// <summary>Verifies plaintext against a BCrypt hash.</summary>
        public bool Verify(string plainText, string hash) =>
            BCrypt.Net.BCrypt.Verify(plainText, hash);
    }
}
