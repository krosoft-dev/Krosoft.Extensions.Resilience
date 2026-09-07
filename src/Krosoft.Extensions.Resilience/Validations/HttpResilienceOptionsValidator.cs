using Krosoft.Extensions.Resilience.Models;
using Microsoft.Extensions.Options;

namespace Krosoft.Extensions.Resilience.Validations;

internal sealed class HttpResilienceOptionsValidator : IValidateOptions<HttpResilienceOptions>
{
    private const string RetryPrefix = nameof(HttpResilienceOptions.Retry);
    private const string CircuitBreakerPrefix = nameof(HttpResilienceOptions.CircuitBreaker);

    private static readonly TimeSpan MaxDuration = TimeSpan.FromDays(1);
    private static readonly TimeSpan MinTimeout = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan MinCircuitBreakerDuration = TimeSpan.FromMilliseconds(500);

    public ValidateOptionsResult Validate(string? name, HttpResilienceOptions options)
    {
        var failures = new List<string>();

        ValidateTimeouts(options, failures);
        ValidateRetry(options.Retry, failures);
        ValidateCircuitBreaker(options, failures);

        if (failures.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(failures.Select(failure => $"{GetSectionLabel(name)} : {failure}"));
    }

    private static string GetSectionLabel(string? name) =>
        string.IsNullOrEmpty(name)
            ? $"Configuration de résilience HTTP invalide (section '{HttpResilienceOptions.SectionName}')"
            : $"Configuration de résilience HTTP invalide pour le client '{name}' (section '{HttpResilienceOptions.SectionName}:{HttpResilienceOptions.ClientsSectionName}:{name}')";

    private static void CheckRange(TimeSpan value, TimeSpan min, TimeSpan max, string label, List<string> failures)
    {
        if (value < min || value > max)
        {
            failures.Add($"'{label}' doit être compris entre {min} et {max}, or il vaut {value}.");
        }
    }

    private static void ValidateTimeouts(HttpResilienceOptions options, List<string> failures)
    {
        CheckRange(options.AttemptTimeout, MinTimeout, MaxDuration, nameof(HttpResilienceOptions.AttemptTimeout), failures);
        CheckRange(options.TotalRequestTimeout, MinTimeout, MaxDuration, nameof(HttpResilienceOptions.TotalRequestTimeout), failures);

        if (options.TotalRequestTimeout < options.AttemptTimeout)
        {
            failures.Add($"'{nameof(HttpResilienceOptions.TotalRequestTimeout)}' ({options.TotalRequestTimeout}) doit être supérieur ou égal à '{nameof(HttpResilienceOptions.AttemptTimeout)}' ({options.AttemptTimeout}).");
        }
    }

    private static void ValidateRetry(HttpRetryOptions retry, List<string> failures)
    {
        if (retry.MaxRetryAttempts < 1)
        {
            failures.Add($"'{RetryPrefix}.{nameof(HttpRetryOptions.MaxRetryAttempts)}' doit être supérieur ou égal à 1, or il vaut {retry.MaxRetryAttempts}. Pour désactiver le retry, utilisez l'extension sans retry (AddResilienceHandlerWithoutRetry).");
        }

        CheckRange(retry.Delay, TimeSpan.Zero, MaxDuration, $"{RetryPrefix}.{nameof(HttpRetryOptions.Delay)}", failures);
    }

    private static void ValidateCircuitBreaker(HttpResilienceOptions options, List<string> failures)
    {
        var circuitBreaker = options.CircuitBreaker;

        if (circuitBreaker.FailureRatio is <= 0 or > 1)
        {
            failures.Add($"'{CircuitBreakerPrefix}.{nameof(HttpCircuitBreakerOptions.FailureRatio)}' doit être compris entre 0 (exclu) et 1 (inclus), or il vaut {circuitBreaker.FailureRatio}.");
        }

        if (circuitBreaker.MinimumThroughput < 2)
        {
            failures.Add($"'{CircuitBreakerPrefix}.{nameof(HttpCircuitBreakerOptions.MinimumThroughput)}' doit être supérieur ou égal à 2, or il vaut {circuitBreaker.MinimumThroughput}.");
        }

        CheckRange(circuitBreaker.SamplingDuration, MinCircuitBreakerDuration, MaxDuration, $"{CircuitBreakerPrefix}.{nameof(HttpCircuitBreakerOptions.SamplingDuration)}", failures);
        CheckRange(circuitBreaker.BreakDuration, MinCircuitBreakerDuration, MaxDuration, $"{CircuitBreakerPrefix}.{nameof(HttpCircuitBreakerOptions.BreakDuration)}", failures);

        var minimumSamplingDuration = options.AttemptTimeout * 2;
        if (circuitBreaker.SamplingDuration < minimumSamplingDuration)
        {
            failures.Add($"'{CircuitBreakerPrefix}.{nameof(HttpCircuitBreakerOptions.SamplingDuration)}' ({circuitBreaker.SamplingDuration}) doit valoir au moins le double de '{nameof(HttpResilienceOptions.AttemptTimeout)}' ({options.AttemptTimeout}), soit {minimumSamplingDuration}, afin que la fenêtre d'observation puisse contenir plusieurs tentatives.");
        }
    }
}
