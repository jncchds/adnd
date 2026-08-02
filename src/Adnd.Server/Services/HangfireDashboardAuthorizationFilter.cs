using System.Net;
using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace Adnd.Server.Services;

/// <summary>
/// Gates the Hangfire dashboard. Hangfire's implicit default is
/// <c>LocalRequestsOnlyAuthorizationFilter</c>, which compares the remote address to the
/// local address — so any deployment with a reverse proxy on the same host makes every
/// external request look local and grants full job control to the internet.
///
/// This denies by default. Set <c>Hangfire:DashboardEnabled=true</c> to serve it, and
/// optionally <c>Hangfire:DashboardAllowedIps</c> (comma-separated) to restrict callers.
/// </summary>
public class HangfireDashboardAuthorizationFilter(IConfiguration config) : IDashboardAuthorizationFilter
{
    private readonly bool _enabled =
        bool.TryParse(config["Hangfire:DashboardEnabled"], out var e) && e;

    private readonly HashSet<string> _allowedIps =
        (config["Hangfire:DashboardAllowedIps"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public bool Authorize([NotNull] DashboardContext context)
    {
        if (!_enabled) return false;

        var httpContext = context.GetHttpContext();

        // An authenticated application user is always acceptable.
        if (httpContext.User?.Identity?.IsAuthenticated == true) return true;

        if (_allowedIps.Count == 0) return false;

        var remote = httpContext.Connection.RemoteIpAddress;
        if (remote is null) return false;

        if (remote.IsIPv4MappedToIPv6) remote = remote.MapToIPv4();

        return _allowedIps.Contains(remote.ToString())
               || (_allowedIps.Contains("localhost") && IPAddress.IsLoopback(remote));
    }
}
