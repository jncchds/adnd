using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Adnd.Server.Services;

namespace Adnd.Server.Controllers;

/// <summary>
/// Authentication endpoints — register, login, token refresh, logout, and profile management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
/// Register a new user account.
/// </summary>
    /// <param name="request">Registration request with email and password.</param>
    /// <returns>Authentication response with JWT token and refresh token.</returns>
    /// <response code="200">User registered successfully.</response>
    /// <response code="400">Invalid email format or password too short.</response>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email and password are required." });
        }

        // Validate email format
        var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@]+@[^@]+\.[^@]+$");
        if (!emailRegex.IsMatch(request.Email))
        {
            return BadRequest(new { error = "Invalid email format." });
        }

        if (request.Password.Length < 8)
        {
            return BadRequest(new { error = "Password must be at least 8 characters." });
        }

        var (response, error) = await _authService.RegisterAsync(request);
        if (error != null)
        {
            return BadRequest(new { error });
        }

        return Ok(response);
    }

    /// <summary>
    /// Authenticate with email and password to receive a JWT token.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <returns>Authentication response with JWT token and refresh token.</returns>
    /// <response code="200">Login successful.</response>
    /// <response code="401">Invalid credentials.</response>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email and password are required." });
        }

        var (response, error) = await _authService.LoginAsync(request);
        if (error != null)
        {
            return Unauthorized(new { error });
        }

        return Ok(response);
    }

    /// <summary>
    /// Refresh an expired JWT token using a valid refresh token.
    /// </summary>
    /// <param name="request">Refresh token.</param>
    /// <returns>New JWT token and refresh token pair.</returns>
    /// <response code="200">Token refreshed successfully.</response>
    /// <response code="401">Invalid or revoked refresh token.</response>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new { error = "Refresh token is required." });
        }

        var (response, error) = await _authService.RefreshTokenAsync(request);
        if (error != null)
        {
            return Unauthorized(new { error });
        }

        return Ok(response);
    }

    /// <summary>
    /// Revoke the current refresh token, effectively logging the user out.
    /// </summary>
    /// <param name="request">Refresh token to revoke.</param>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await _authService.RevokeTokenAsync(request.RefreshToken);
        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Get the current authenticated user's profile information.
    /// </summary>
    /// <returns>User profile with id, email, display name, and creation date.</returns>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        var user = await _authService.GetUserByIdAsync(id);
        if (user == null)
        {
            return Unauthorized();
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.CreatedAt
        });
    }

    /// <summary>
    /// Change the current user's password.
    /// </summary>
    /// <param name="request">Current and new password.</param>
    /// <response code="200">Password changed successfully.</response>
    /// <response code="400">Current password is incorrect.</response>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new { error = "Current password and new password are required." });
        }

        var (success, errorMessage) = await _authService.ChangePasswordAsync(id, request.CurrentPassword, request.NewPassword);
        if (!success)
        {
            return BadRequest(new { error = errorMessage });
        }

        return Ok(new { message = "Password changed successfully." });
    }

    /// <summary>
    /// Update the current user's display name.
    /// </summary>
    /// <param name="request">New display name.</param>
    /// <response code="200">Display name updated successfully.</response>
    /// <response code="400">Display name is empty.</response>
    [HttpPut("display-name")]
    [Authorize]
    public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var id))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { error = "Display name cannot be empty." });
        }

        var (success, errorMessage) = await _authService.UpdateDisplayNameAsync(id, request.DisplayName);
        if (!success)
        {
            return BadRequest(new { error = errorMessage });
        }

        return Ok(new { message = "Display name updated successfully." });
    }
}

/// <summary>
/// Request to change a user's password.
/// </summary>
public class ChangePasswordRequest
{
    /// <summary>Current password.</summary>
    public string CurrentPassword { get; set; } = string.Empty;
    /// <summary>New password (minimum 8 characters).</summary>
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Request to update a user's display name.
/// </summary>
public class UpdateDisplayNameRequest
{
    /// <summary>New display name (non-empty).</summary>
    public string DisplayName { get; set; } = string.Empty;
}
