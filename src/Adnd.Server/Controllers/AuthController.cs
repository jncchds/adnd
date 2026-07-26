using Adnd.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adnd.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService, IUserIdProvider userIdProvider) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        try
        {
            var result = await authService.RegisterAsync(req.Email, req.Password, req.DisplayName);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        try
        {
            var result = await authService.LoginAsync(req.Email, req.Password);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
    {
        try
        {
            var result = await authService.RefreshAsync(req.RefreshToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Invalid or expired refresh token." });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
    {
        await authService.LogoutAsync(req.RefreshToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = userIdProvider.GetUserId();
        var user = await authService.GetMeAsync(userId);
        return Ok(user);
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        try
        {
            var userId = userIdProvider.GetUserId();
            await authService.ChangePasswordAsync(userId, req.CurrentPassword, req.NewPassword);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { error = "Current password is incorrect." });
        }
    }

    [Authorize]
    [HttpPut("display-name")]
    public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameRequest req)
    {
        var userId = userIdProvider.GetUserId();
        await authService.UpdateDisplayNameAsync(userId, req.DisplayName);
        return NoContent();
    }
}

public record RegisterRequest(string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record UpdateDisplayNameRequest(string DisplayName);
