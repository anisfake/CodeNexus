using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Entities;
using CodeNexus.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace CodeNexus.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly JwtSettings _settings;

    public TokenService(IOptions<JwtSettings> settings)
    {
        _settings = settings.Value;
    }

    public int RefreshTokenExpirationDays => _settings.RefreshTokenExpirationDays;

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Username)
        };

        if (user.RoleId.HasValue)
            claims.Add(new Claim("roleId", user.RoleId.Value.ToString()));

        if (user.Role != null && !string.IsNullOrWhiteSpace(user.Role.RoleName))
            claims.Add(new Claim(ClaimTypes.Role, user.Role.RoleName));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public string HashRefreshToken(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public string GenerateResetPasswordToken(string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim("purpose", "ResetPassword")
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ResetPasswordTokenExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string? ValidateResetPasswordToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));

            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidIssuer = _settings.Issuer,
                ValidAudience = _settings.Audience,
                IssuerSigningKey = key
            };

            var principal = handler.ValidateToken(token, parameters, out _);

            var purpose = principal.FindFirst("purpose")?.Value;
            if (purpose != "ResetPassword") return null;

            return principal.FindFirst(ClaimTypes.Email)?.Value;
        }
        catch
        {
            return null;
        }
    }

    public (string? TokenId, DateTime? ExpiresAt) ExtractTokenInfo(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();

            if (!handler.CanReadToken(token))
                return (null, null);

            var jwtToken = handler.ReadJwtToken(token);
            var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
            var exp = jwtToken.ValidTo;

            return (jti, exp);
        }
        catch
        {
            return (null, null);
        }
    }
}
