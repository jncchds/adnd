using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Adnd.Server.Services;

/// <summary>
/// Centralized resilience policies for LLM provider calls.
/// Provides retry with exponential backoff and circuit breaker patterns.
/// </summary>
public interface IResiliencePolicies
{
    /// <summary>
    /// Retry policy for LLM API calls. Retries transient failures (5xx, timeouts) with exponential backoff.
    /// </summary>
    IAsyncPolicy GetRetryPolicy();

    /// <summary>
    /// Circuit breaker policy for LLM API calls. Opens after consecutive failures, half-opens after timeout.
    /// </summary>
    IAsyncPolicy GetCircuitBreakerPolicy();

    /// <summary>
    /// Combined policy: circuit breaker wrapping retry.
    /// </summary>
    IAsyncPolicy GetCombinedPolicy();
}

public class ResiliencePolicies : IResiliencePolicies
{
    private readonly ILogger<ResiliencePolicies> _logger;
    private readonly double _retryDelayMs;
    private readonly int _maxRetries;
    private readonly int _failureThreshold;
    private readonly TimeSpan _halfOpenAfter;

    // Cached policies — singleton-scoped to preserve circuit breaker state across calls
    private AsyncRetryPolicy? _retryPolicy;
    private AsyncCircuitBreakerPolicy? _circuitBreakerPolicy;
    private IAsyncPolicy? _combinedPolicy;

    public ResiliencePolicies(
        ILogger<ResiliencePolicies> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _retryDelayMs = configuration.GetValue<int>("Resilience:RetryDelayMs", 500);
        _maxRetries = configuration.GetValue<int>("Resilience:MaxRetries", 3);
        _failureThreshold = configuration.GetValue<int>("Resilience:FailureThreshold", 5);
        _halfOpenAfter = TimeSpan.FromSeconds(configuration.GetValue<int>("Resilience:HalfOpenAfterSec", 30));
    }

    public IAsyncPolicy GetRetryPolicy()
    {
        // Max retry delay cap at 30 seconds
        const double maxDelayMs = 30000.0;
        return _retryPolicy ??= Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .Or<TaskCanceledException>()
            .Or<SocketException>()
            .WaitAndRetryAsync(
                _maxRetries,
                attempt => TimeSpan.FromMilliseconds(Math.Min(maxDelayMs, _retryDelayMs * Math.Pow(2, attempt))),
                onRetry: (_, span, attempt, _) =>
                {
                    _logger.LogWarning(
                        "LLM call retry attempt {Attempt}. Waiting {Delay}ms.",
                        attempt, span.TotalMilliseconds);
                });
    }

    public IAsyncPolicy GetCircuitBreakerPolicy()
    {
        return _circuitBreakerPolicy ??= Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .CircuitBreakerAsync(
                _failureThreshold,
                _halfOpenAfter,
                onBreak: (ex, duration) =>
                {
                    _logger.LogError(
                        ex,
                        "Circuit breaker OPEN for LLM provider. Will half-open after {Duration}.",
                        duration);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker RESET for LLM provider. Resuming calls.");
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker HALF-OPEN for LLM provider. Testing with next call.");
                });
    }

    public IAsyncPolicy GetCombinedPolicy()
    {
        // Retry should be OUTER, circuit breaker INNER — retry first, then check circuit
        return _combinedPolicy ??= Policy.WrapAsync(GetRetryPolicy(), GetCircuitBreakerPolicy());
    }
}

/// <summary>
/// Extension methods for applying resilience policies to LLM calls.
/// </summary>
public static class ResilienceExtensions
{
    /// <summary>
    /// Execute an LLM call with resilience (retry + circuit breaker).
    /// Returns a Result wrapping the outcome, enabling easy error handling.
    /// </summary>
    public static async Task<Result> ExecuteWithResilienceAsync(
        this IAsyncPolicy policy,
        Func<Task<string>> action,
        string fallbackValue = "LLM service unavailable after retries. Please try again later.")
    {
        try
        {
            var result = await policy.ExecuteAsync(action);
            return Result.Success(result);
        }
        catch (BrokenCircuitException)
        {
            return Result.Failure(fallbackValue);
        }
        catch (Exception ex)
        {
            // Include full exception type and message — don't lose diagnostic info
            return Result.Failure($"Error ({ex.GetType().Name}): {ex.Message}");
        }
    }

    /// <summary>
    /// Simple result wrapper for resilience handling.
    /// </summary>
    public readonly struct Result
    {
        public string Value { get; }
        public bool IsSuccess { get; }
        public string? Error { get; }

        private Result(string value, bool isSuccess, string? error)
        {
            Value = value;
            IsSuccess = isSuccess;
            Error = error;
        }

        public static Result Success(string value) => new(value, true, null);
        public static Result Failure(string error) => new(error, false, error);
    }
}
