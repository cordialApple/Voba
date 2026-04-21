using Voba.Interfaces;

namespace Voba.Services
{
    public class BcryptPasswordHasher : IPasswordHasher
    {
        public string GenerateSalt() =>
            BCrypt.Net.BCrypt.GenerateSalt();

        public string Hash(string plainText, string salt) =>
            BCrypt.Net.BCrypt.HashPassword(plainText, salt);

        public bool Verify(string plainText, string hash) =>
            BCrypt.Net.BCrypt.Verify(plainText, hash);
    }
}
