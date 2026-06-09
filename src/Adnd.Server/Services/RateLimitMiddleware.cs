using System.Collections.Concurrent;
using Microsoft.Extensions.Primitives;

namespace Adnd.Server.Services;

/// <summary>
/// Simple IP-based rate limiter middleware for API endpoints.
/// Uses a sliding window approach with in-memory tracking.
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private readonly ConcurrentDictionary<string, List<DateTime>> _requestCounts = new();
    private readonly object _lock = new();

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // Determine rate limit policy based on route
        var (limit, window) = path switch
        {
            _ when path.Contains("/api/auth/") => (10, TimeSpan.FromMinutes(1)),
            _ when path.Contains("/api/llm-presets") => (30, TimeSpan.FromMinutes(1)),
            _ => (100, TimeSpan.FromMinutes(1))
        };

        // Get client IP
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var now = DateTime.UtcNow;
        var windowStart = now - window;

        List<DateTime>? timestamps;
        bool exceeded;

        lock (_lock)
        {
            if (!_requestCounts.TryGetValue(ip, out timestamps!))
            {
                timestamps = new List<DateTime>();
                _requestCounts[ip] = timestamps;
            }

            // Remove expired entries
            timestamps.RemoveAll(t => t < windowStart);

            exceeded = timestamps.Count >= limit;
            if (!exceeded)
            {
                timestamps.Add(now);
            }
        }

        if (exceeded)
        {
            _logger.LogWarning("Rate limit exceeded for IP {Ip} on path {Path}", ip, path);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = ((int)window.TotalSeconds).ToString();
            await context.Response.WriteAsync("{\"error\": \"Rate limit exceeded. Please try again later.\"}");
            return;
        }

        await _next(context);
    }
}

public static class RateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitMiddleware>();
    }
}
