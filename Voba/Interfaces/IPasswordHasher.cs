namespace Voba.Interfaces
{
    public interface IPasswordHasher
    {
        /// <summary>Generates a new random salt.</summary>
        string GenerateSalt();

        /// <summary>Hashes plaintext using the provided salt.</summary>
        string Hash(string plainText, string salt);

        /// <summary>Verifies plaintext against a stored hash.</summary>
        bool Verify(string plainText, string hash);
    }
}
