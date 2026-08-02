using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Adnd.Server.Data;
using Adnd.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Adnd.Server.Services;

public record AuthResult(string Token, string RefreshToken, UserDto User);
public record UserDto(Guid Id, string Email, string DisplayName);

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string displayName);
    Task<AuthResult> LoginAsync(string email, string password);
    Task<AuthResult> RefreshAsync(string refreshToken);
    Task LogoutAsync(string refreshToken);
    Task<UserDto> GetMeAsync(Guid userId);
    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task UpdateDisplayNameAsync(Guid userId, string displayName);
}

public class AuthService(AppDbContext db, IConfiguration configuration) : IAuthService
{
    private readonly string _secretKey = configuration["JwtSettings:SecretKey"]
        ?? throw new InvalidOperationException("JwtSettings:SecretKey not configured.");
    private readonly string _issuer = configuration["JwtSettings:Issuer"]
        ?? throw new InvalidOperationException("JwtSettings:Issuer not configured.");
    private readonly string _audience = configuration["JwtSettings:Audience"]
        ?? throw new InvalidOperationException("JwtSettings:Audience not configured.");

    /// <summary>Trim as well as lower-case, so " A@b.com" and "a@b.com" are one account.</summary>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<AuthResult> RegisterAsync(string email, string password, string displayName)
    {
        var normalized = NormalizeEmail(email);

        if (await db.Users.AnyAsync(u => u.Email == normalized))
            throw new InvalidOperationException("Email already registered.");

        var user = new User
        {
            Email = normalized,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = displayName.Trim(),
            LastLoginAt = DateTimeOffset.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return await IssueTokensAsync(user);
    }

    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var normalized = NormalizeEmail(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return await IssueTokensAsync(user);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken)
    {
        var token = await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken);

        if (token is null || token.ExpiresAt < DateTimeOffset.UtcNow)
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");

        // Reuse detection: presenting an already-revoked token means it leaked, so revoke
        // every token for that user rather than just rejecting this one request.
        if (token.IsRevoked)
        {
            await RevokeAllForUserAsync(token.UserId);
            throw new UnauthorizedAccessException("Refresh token has already been used.");
        }

        token.IsRevoked = true;
        await db.SaveChangesAsync();

        return await IssueTokensAsync(token.User);
    }

    private async Task RevokeAllForUserAsync(Guid userId)
    {
        await db.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true));
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var token = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
        if (token is not null)
        {
            token.IsRevoked = true;
            await db.SaveChangesAsync();
        }
    }

    public async Task<UserDto> GetMeAsync(Guid userId)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");
        return new UserDto(user.Id, user.Email, user.DisplayName);
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await db.SaveChangesAsync();

        // Changing a password is usually a response to compromise, so evict every other
        // session. Previously the attacker's 30-day refresh token kept working.
        await RevokeAllForUserAsync(userId);
    }

    public async Task UpdateDisplayNameAsync(Guid userId, string displayName)
    {
        var user = await db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");
        user.DisplayName = displayName;
        await db.SaveChangesAsync();
    }

    private async Task<AuthResult> IssueTokensAsync(User user)
    {
        var accessToken = GenerateJwt(user);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AuthResult(accessToken, refreshToken, new UserDto(user.Id, user.Email, user.DisplayName));
    }

    private string GenerateJwt(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> CreateRefreshTokenAsync(Guid userId)
    {
        var token = new RefreshToken
        {
            UserId = userId,
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
        };
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync();
        return token.Token;
    }
}
