using Voba.Models;

namespace Voba.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(User user);
        bool ValidateToken(string token);
        string? GetUserIdFromToken(string token);
    }
}
