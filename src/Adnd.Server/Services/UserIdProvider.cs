using System.Security.Claims;

namespace Adnd.Server.Services;

/// <summary>
/// Extracts the current user's GUID from the HTTP context.
/// Replaces duplicated GetCurrentUserId() across controllers.
/// </summary>
public interface IUserIdProvider
{
    Guid GetCurrentUserId();
    Guid? TryGetCurrentUserId();
}

public class UserIdProvider : IUserIdProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserIdProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HTTP context available.");

        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var id))
            throw new UnauthorizedAccessException("User identity not found or invalid.");

        return id;
    }

    public Guid? TryGetCurrentUserId()
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HTTP context available.");

        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null || !Guid.TryParse(userId, out var id))
            return null;

        return id;
    }
}
