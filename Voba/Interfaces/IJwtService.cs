using Voba.Models;

namespace Voba.Interfaces
{
    public interface IJwtService
    {
        /// <summary>Generates a signed JWT for the given user.</summary>
        string GenerateToken(User user);

        /// <summary>Returns true if the token signature and expiry are valid.</summary>
        bool ValidateToken(string token);

        /// <summary>Extracts the user Id from the token, or null if invalid.</summary>
        string? GetUserIdFromToken(string token);
    }
}
