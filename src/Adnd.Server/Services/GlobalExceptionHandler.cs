using System.Text.Json;

namespace Adnd.Server.Services;

/// <summary>
/// Converts unhandled exceptions into a consistent JSON error without leaking internals.
/// The app previously had no exception handling middleware at all, so stack traces — and,
/// with "Include Error Detail=true" in the connection string, Npgsql parameter values and
/// row data — could surface in 500 response bodies.
/// </summary>
public static class GlobalExceptionHandlerExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.Use(async (ctx, next) =>
        {
            try
            {
                await next();
            }
            catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
            {
                // The client went away; nothing to report.
            }
            catch (Exception ex)
            {
                var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Adnd.Server.UnhandledException");

                logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

                if (ctx.Response.HasStarted) throw;

                var (status, message) = ex switch
                {
                    KeyNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                    UnauthorizedAccessException => (StatusCodes.Status403Forbidden, ex.Message),
                    ArgumentException => (StatusCodes.Status400BadRequest, ex.Message),
                    InvalidOperationException => (StatusCodes.Status400BadRequest, ex.Message),
                    _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
                };

                ctx.Response.Clear();
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
            }
        });
    }
}
