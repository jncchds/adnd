using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Adnd.Server.Models;
using Adnd.Server.Services;

namespace Adnd.Server.Controllers;

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

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        await _authService.RevokeTokenAsync(request.RefreshToken);
        return Ok(new { message = "Logged out successfully." });
    }

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
}
