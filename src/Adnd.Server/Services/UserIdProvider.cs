using System.Security.Claims;

namespace Adnd.Server.Services;

public interface IUserIdProvider
{
    Guid GetUserId();
    Guid? TryGetUserId();
}

public class UserIdProvider(IHttpContextAccessor httpContextAccessor) : IUserIdProvider
{
    public Guid GetUserId()
    {
        var id = TryGetUserId();
        if (id is null)
            throw new UnauthorizedAccessException("User is not authenticated.");
        return id.Value;
    }

    public Guid? TryGetUserId()
    {
        var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return value is not null && Guid.TryParse(value, out var id) ? id : null;
    }
}
