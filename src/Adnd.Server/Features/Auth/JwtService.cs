using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Adnd.Server.Features.Auth;

public class JwtService
{
    private readonly string _secretKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _accessExpiration;
    private readonly TimeSpan _refreshExpiration;

    public JwtService(IConfiguration config)
    {
        _secretKey = config["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JWT secret not configured");
        _issuer = config["JwtSettings:Issuer"] ?? "adnd-server";
        _audience = config["JwtSettings:Audience"] ?? "adnd-client";
        _accessExpiration = TimeSpan.FromMinutes(15);
        _refreshExpiration = TimeSpan.FromDays(7);
    }

    public (string AccessToken, string RefreshToken, DateTime ExpiresAt) GenerateTokens(Guid userId, string email, string displayName)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim("displayName", displayName)
        };

        var accessToken = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(_accessExpiration),
            signingCredentials: credentials);

        var refreshToken = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            expires: DateTime.UtcNow.Add(_refreshExpiration),
            signingCredentials: credentials);

        return (
            new JwtSecurityTokenHandler().WriteToken(accessToken),
            new JwtSecurityTokenHandler().WriteToken(refreshToken),
            accessToken.ValidTo
        );
    }
}
