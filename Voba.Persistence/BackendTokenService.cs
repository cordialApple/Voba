using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Voba.Services;

public sealed class BackendTokenService
{
    private readonly SymmetricSecurityKey _key;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeProvider _clock;
    private readonly TokenValidationParameters _validation;

    public BackendTokenService(string signingKeyBase64, string issuer, string audience,
        TimeProvider? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signingKeyBase64);
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(signingKeyBase64);
        }
        catch (FormatException error)
        {
            throw new InvalidOperationException("JWT signing key must be Base64 encoded.", error);
        }
        if (bytes.Length < 32)
            throw new InvalidOperationException("JWT signing key must contain at least 32 bytes.");

        _key = new SymmetricSecurityKey(bytes);
        _issuer = issuer;
        _audience = audience;
        _clock = clock ?? TimeProvider.System;
        _validation = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _key,
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = _clock.GetUtcNow().UtcDateTime;
                return expires.HasValue && expires > now &&
                    (!notBefore.HasValue || notBefore <= now);
            },
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
    }

    public string IssueAccessToken(string userId, string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var now = _clock.GetUtcNow().UtcDateTime;
        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim("sid", sessionId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            ],
            notBefore: now,
            expires: now.AddMinutes(15),
            signingCredentials: new SigningCredentials(_key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string UserId, string SessionId)? ValidateAccessToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, _validation, out _);
            var userId = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var sessionId = principal.FindFirst("sid")?.Value;
            return string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId)
                ? null : (userId, sessionId);
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
