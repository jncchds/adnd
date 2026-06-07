using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Adnd.Server.Models;
using Adnd.Server.Data;

namespace Adnd.Server.Services;

public interface IAuthService
{
    Task<(AuthResponse? response, string? errorMessage)> RegisterAsync(RegisterRequest request);
    Task<(AuthResponse? response, string? errorMessage)> LoginAsync(LoginRequest request);
    Task<(AuthResponse? response, string? errorMessage)> RefreshTokenAsync(RefreshRequest request);
    Task<bool> RevokeTokenAsync(string refreshToken);
    Task<User?> GetUserByIdAsync(Guid userId);
    Task<User?> GetUserByEmailAsync(string email);
}

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext context, IConfiguration configuration, ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<(AuthResponse? response, string? errorMessage)> RegisterAsync(RegisterRequest request)
    {
        // Check if email already exists
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
        {
            return (null!, "Email already registered.");
        }

        // Create user
        var user = new User
        {
            Email = request.Email.ToLower().Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Generate tokens
        var (accessToken, refreshToken) = GenerateTokens(user);

        // Save refresh token
        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return (new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = user
        }, null);
    }

    public async Task<(AuthResponse? response, string? errorMessage)> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower().Trim());
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return (null!, "Invalid email or password.");
        }

        // Generate tokens
        var (accessToken, refreshToken) = GenerateTokens(user);

        // Save refresh token
        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _context.RefreshTokens.Add(refreshTokenEntity);
        await _context.SaveChangesAsync();

        return (new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = user
        }, null);
    }

    public async Task<(AuthResponse? response, string? errorMessage)> RefreshTokenAsync(RefreshRequest request)
    {
        var refreshTokenEntity = await _context.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == request.RefreshToken && !r.IsRevoked);

        if (refreshTokenEntity == null || refreshTokenEntity.ExpiresAt < DateTime.UtcNow)
        {
            return (null!, "Invalid or expired refresh token.");
        }

        var user = await _context.Users.FindAsync(refreshTokenEntity.UserId);
        if (user == null)
        {
            return (null!, "User not found.");
        }

        // Revoke old refresh token
        refreshTokenEntity.IsRevoked = true;
        refreshTokenEntity.RevokedAt = DateTime.UtcNow;

        // Generate new tokens
        var (accessToken, newRefreshToken) = GenerateTokens(user);

        // Save new refresh token
        var newRefreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        _context.RefreshTokens.Add(newRefreshTokenEntity);
        await _context.SaveChangesAsync();

        return (new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            User = user
        }, null);
    }

    public async Task<bool> RevokeTokenAsync(string refreshToken)
    {
        var tokenEntity = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken && !t.IsRevoked);

        if (tokenEntity == null) return false;

        tokenEntity.IsRevoked = true;
        tokenEntity.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<User?> GetUserByIdAsync(Guid userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower().Trim());
    }

    private (string accessToken, string refreshToken) GenerateTokens(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var signingCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName ?? user.Email),
            new Claim("UserId", user.Id.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: signingCredentials
        );

        // Generate refresh token
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        return (new JwtSecurityTokenHandler().WriteToken(token), refreshToken);
    }
}
