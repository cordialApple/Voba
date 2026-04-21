namespace Voba.Interfaces
{
    public interface IPasswordHasher
    {
        string GenerateSalt();
        string Hash(string plainText, string salt);
        bool Verify(string plainText, string hash);
    }
}
