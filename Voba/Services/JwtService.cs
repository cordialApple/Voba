using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Voba.Interfaces;
using Voba.Models;

namespace Voba.Services
{
    public class JwtService : IJwtService
    {
        private readonly SymmetricSecurityKey _signingKey;
        private readonly TokenValidationParameters _validationParameters;

        public JwtService()
        {
            var keyBytes = Convert.FromBase64String(Secrets.JwtSecret);
            _signingKey = new SymmetricSecurityKey(keyBytes);

            _validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = _signingKey,
                ValidateIssuer           = false,
                ValidateAudience         = false,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero
            };
        }

        /// <summary>Generates a signed JWT for the given user.</summary>
        public string GenerateToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                claims:  claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>Returns true if the token signature and expiry are valid.</summary>
        public bool ValidateToken(string token)
        {
            try
            {
                new JwtSecurityTokenHandler().ValidateToken(token, _validationParameters, out _);
                return true;
            }
            catch (SecurityTokenException)
            {
                return false;
            }
        }

        /// <summary>Extracts the user Id from the token, or null if invalid.</summary>
        public string? GetUserIdFromToken(string token)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, _validationParameters, out _);
                return principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            }
            catch (SecurityTokenException)
            {
                return null;
            }
        }
    }
}
