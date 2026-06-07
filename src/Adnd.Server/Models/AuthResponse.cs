namespace Adnd.Server.Models;

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public User User { get; set; } = null!;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class CreateGameRequest
{
    public string Name { get; set; } = string.Empty;
    public string SystemId { get; set; } = "dnd5e";
    public string? SystemVersion { get; set; }
    public string? CustomSystemJson { get; set; }
    public string? PlotSeed { get; set; }       // Initial plot setup by creator
    public string? GameParameters { get; set; }  // Game tone, difficulty, pacing
}

public class GameResponse
{
    public Guid Id { get; set; }
    public Guid CreatorId { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public Guid? GameMasterId { get; set; }
    public string? GameMasterName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SystemId { get; set; } = string.Empty;
    public string? SystemVersion { get; set; }
    public GameStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? InviteCode { get; set; }
    public string? PlotSeed { get; set; }
    public string? GameParameters { get; set; }
    public string? GameState { get; set; }
}

public class InviteResponse
{
    public string InviteCode { get; set; } = string.Empty;
    public string InviteUrl { get; set; } = string.Empty;
}

public class AssignGMRequest
{
    public Guid PlayerId { get; set; }
}
