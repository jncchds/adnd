using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Adnd.Server.Shared;
using Adnd.Server.Features.Auth.Dto;
using BCrypt.Net;
using System.Security.Claims;

namespace Adnd.Server.Features.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;

    public AuthController(AppDbContext db, JwtService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict(new { error = "Email already registered" });

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        // Generate tokens and save refresh token
        var (accessToken, refreshToken, expiresAt) = _jwt.GenerateTokens(user.Id, user.Email, user.DisplayName);
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.Add(TimeSpan.FromDays(7));
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid credentials" });

        var (accessToken, refreshToken, expiresAt) = _jwt.GenerateTokens(user.Id, user.Email, user.DisplayName);
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.Add(TimeSpan.FromDays(7));
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt
        });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
            return Unauthorized(new { error = "Refresh token required" });

        var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);
        if (user == null || user.RefreshTokenExpiry <= DateTime.UtcNow)
            return Unauthorized(new { error = "Invalid refresh token" });

        var (accessToken, newRefreshToken, expiresAt) = _jwt.GenerateTokens(user.Id, user.Email, user.DisplayName);
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.Add(TimeSpan.FromDays(7));
        await _db.SaveChangesAsync();

        return Ok(new AuthResponse
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = expiresAt
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(Guid.Parse(userId));
        if (user != null)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;
            await _db.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var user = await _db.Users.FindAsync(Guid.Parse(userId));
        if (user == null) return NotFound();

        return Ok(new { user.Id, user.Email, user.DisplayName });
    }
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
