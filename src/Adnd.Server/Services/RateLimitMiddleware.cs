using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.RateLimiting;

namespace Adnd.Server.Services;

/// <summary>
/// Configuration for rate limiting settings.
/// </summary>
public class RateLimitingOptions
{
    /// <summary>
    /// Whether to extract client IP from X-Forwarded-For headers (for reverse proxy support).
    /// </summary>
    public bool UseForwardedHeaders { get; set; } = false;

    /// <summary>
    /// List of trusted proxy IPs that are allowed to set X-Forwarded-For.
    /// If empty and TrustAllProxies is false, no proxies are trusted.
    /// </summary>
    public List<string> TrustedProxies { get; set; } = new();

    /// <summary>
    /// Trust all proxies — useful for Docker Compose where proxy IPs are dynamic.
    /// When true, any connection can set X-Forwarded-For.
    /// Only use in trusted/internal networks (Docker Compose, k8s, etc.).
    /// </summary>
    public bool TrustAllProxies { get; set; } = false;

    /// <summary>
    /// Headers to check for forwarded IP, in priority order.
    /// </summary>
    public List<string> ForwardedHeaders { get; set; } = new() { "X-Forwarded-For", "X-Real-IP" };

    /// <summary>
    /// Whether to use ASP.NET Core's built-in rate limiter (recommended) or the legacy middleware.
    /// </summary>
    public bool UseAspNetCoreRateLimiter { get; set; } = true;

    /// <summary>
    /// Global default: max requests per window.
    /// </summary>
    public int GlobalLimit { get; set; } = 100;

    /// <summary>
    /// Global default: window duration.
    /// </summary>
    public int GlobalWindowMinutes { get; set; } = 1;

    /// <summary>
    /// Auth endpoints limit (register/login/refresh/logout). /auth/me is excluded from rate limiting.
    /// </summary>
    public int AuthLimit { get; set; } = 30;

    /// <summary>
    /// Auth endpoints window in minutes.
    /// </summary>
    public int AuthWindowMinutes { get; set; } = 1;

    /// <summary>
    /// LLM preset management limit.
    /// </summary>
    public int LlmPresetLimit { get; set; } = 30;

    /// <summary>
    /// LLM preset window in minutes.
    /// </summary>
    public int LlmPresetWindowMinutes { get; set; } = 1;

    /// <summary>
    /// Endpoints to exclude from rate limiting.
    /// </summary>
    public List<string> ExcludedPaths { get; set; } = new()
    {
        "/health",
        "/health/ready",
        "/swagger",
        "/swagger/",
        "/favicon.ico",
        "/api/auth/me"       // Auth validation — must never be rate-limited (auth hook calls it on every mount)
    };
}

/// <summary>
/// Simple IP-based rate limiter middleware for API endpoints.
/// Uses a sliding window approach with in-memory tracking.
/// Supports reverse proxies via X-Forwarded-For headers.
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private readonly RateLimitingOptions _options;
    private readonly ConcurrentDictionary<string, List<DateTime>> _requestCounts = new();
    private readonly object _lock = new();
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);
    private DateTime _lastCleanup = DateTime.UtcNow;

    public RateLimitMiddleware(
        RequestDelegate next,
        ILogger<RateLimitMiddleware> logger,
        IOptions<RateLimitingOptions> options)
    {
        _next = next;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check exclusion list first — fast path
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        if (IsExcluded(path))
        {
            await _next(context);
            return;
        }

        // Get client IP (respects reverse proxy)
        var ip = GetClientIp(context);

        // Determine rate limit policy based on route
        var (limit, window) = GetPolicy(path);

        var now = DateTime.UtcNow;
        var windowStart = now - window;

        // Periodic cleanup to prevent memory leaks
        if ((now - _lastCleanup) > _cleanupInterval)
        {
            CleanupExpired();
            _lastCleanup = now;
        }

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
            _logger.LogWarning(
                "Rate limit exceeded for IP {Ip} on path {Path} (limit={Limit}, window={Window}min)",
                ip, path, limit, window.TotalMinutes);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers.RetryAfter = ((int)window.TotalSeconds).ToString();
            context.Response.Headers["Content-Type"] = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Rate limit exceeded. Please try again later.\"}");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Get the real client IP, respecting reverse proxy headers.
    /// </summary>
    private string GetClientIp(HttpContext context)
    {
        if (!_options.UseForwardedHeaders)
        {
            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        // Check forwarded headers in priority order
        foreach (var headerName in _options.ForwardedHeaders)
        {
            if (context.Request.Headers.TryGetValue(headerName, out var headerValue) &&
                !string.IsNullOrEmpty(headerValue))
            {
                // X-Forwarded-For can contain multiple IPs: client, proxy1, proxy2
                // The first IP is the original client
                var firstIp = headerValue.ToString().Split(',')[0].Trim();
                if (!string.IsNullOrEmpty(firstIp) && IsValidIp(firstIp))
                {
                    // If we have trusted proxies (and aren't trusting all), verify the last known proxy
                    if (!_options.TrustAllProxies && _options.TrustedProxies.Count > 0)
                    {
                        var lastProxy = context.Connection.RemoteIpAddress?.ToString();
                        if (!string.IsNullOrEmpty(lastProxy) && !_options.TrustedProxies.Contains(lastProxy))
                        {
                            _logger.LogWarning(
                                "Untrusted proxy {ProxyIp} attempted to set {Header}. Using remote IP instead.",
                                lastProxy, headerName);
                            continue;
                        }
                    }
                    return firstIp;
                }
            }
        }

        // Fallback to remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static bool IsValidIp(string ip)
    {
        return IPAddress.TryParse(ip, out _);
    }

    private bool IsExcluded(string path)
    {
        // Check configured exclusion list first
        foreach (var excluded in _options.ExcludedPaths)
        {
            if (path.Equals(excluded, StringComparison.Ordinal) || path.StartsWith(excluded + "/", StringComparison.Ordinal))
            {
                return true;
            }
        }

        // Built-in exclusions (always excluded)
        return path.StartsWith("/health", StringComparison.Ordinal)
            || path.StartsWith("/swagger", StringComparison.Ordinal)
            || path == "/favicon.ico";
    }

    private (int limit, TimeSpan window) GetPolicy(string path)
    {
        return path switch
        {
            _ when path.Contains("/api/auth/") => (_options.AuthLimit, TimeSpan.FromMinutes(_options.AuthWindowMinutes)),
            _ when path.Contains("/api/llm-presets") => (_options.LlmPresetLimit, TimeSpan.FromMinutes(_options.LlmPresetWindowMinutes)),
            _ => (_options.GlobalLimit, TimeSpan.FromMinutes(_options.GlobalWindowMinutes))
        };
    }

    /// <summary>
    /// Clean up expired entries from all IPs to prevent memory leaks.
    /// </summary>
    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        var keysToRemove = new List<string>();

        foreach (var entry in _requestCounts)
        {
            entry.Value.RemoveAll(t => t < now - TimeSpan.FromMinutes(_options.GlobalWindowMinutes));
            if (entry.Value.Count == 0)
            {
                keysToRemove.Add(entry.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _requestCounts.TryRemove(key, out _);
        }
    }
}

public static class RateLimitMiddlewareExtensions
{
    /// <summary>
    /// Add rate limiting configuration options.
    /// </summary>
    public static IServiceCollection AddRateLimitingOptions(this IServiceCollection services)
    {
        services.Configure<RateLimitingOptions>(options =>
        {
            // Production defaults
            if (options.TrustedProxies.Count == 0)
            {
                options.UseForwardedHeaders = true;
                // Common reverse proxy IPs
                options.TrustedProxies.Add("127.0.0.1");
                options.TrustedProxies.Add("::1");
                options.TrustedProxies.Add("10.0.0.0/8");     // Private network
                options.TrustedProxies.Add("172.16.0.0/12");   // Private network
                options.TrustedProxies.Add("192.168.0.0/16");   // Private network
            }
        });
        return services;
    }

    /// <summary>
    /// Configure forwarded headers middleware for reverse proxy support.
    /// Call this BEFORE UseRateLimiting() if UseForwardedHeaders is enabled.
    /// </summary>
    public static IApplicationBuilder ConfigureForwardedHeaders(this IApplicationBuilder app)
    {
        return app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            // Kestrel strips these headers by default. We need to explicitly allow them.
            // In production, set TrustedProxies to your reverse proxy IPs.
            RequireHeaderSymmetry = false,
        });
    }

    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitMiddleware>();
    }
}
