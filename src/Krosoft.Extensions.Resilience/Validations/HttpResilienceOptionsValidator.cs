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

        ValidatePipelineNotEmpty(options, failures);
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

    private static void ValidatePipelineNotEmpty(HttpResilienceOptions options, List<string> failures)
    {
        if (!options.TotalRequestTimeout.Enabled
            && !options.AttemptTimeout.Enabled
            && !options.Retry.Enabled
            && !options.CircuitBreaker.Enabled)
        {
            failures.Add("Au moins une stratégie de résilience doit être activée (TotalRequestTimeout, AttemptTimeout, Retry ou CircuitBreaker) : un pipeline vide n'est pas autorisé.");
        }
    }

    private static void ValidateTimeouts(HttpResilienceOptions options, List<string> failures)
    {
        if (options.AttemptTimeout.Enabled)
        {
            CheckRange(options.AttemptTimeout.Timeout, MinTimeout, MaxDuration, nameof(HttpResilienceOptions.AttemptTimeout), failures);
        }

        if (options.TotalRequestTimeout.Enabled)
        {
            CheckRange(options.TotalRequestTimeout.Timeout, MinTimeout, MaxDuration, nameof(HttpResilienceOptions.TotalRequestTimeout), failures);
        }

        if (options.TotalRequestTimeout.Enabled
            && options.AttemptTimeout.Enabled
            && options.TotalRequestTimeout.Timeout < options.AttemptTimeout.Timeout)
        {
            failures.Add($"'{nameof(HttpResilienceOptions.TotalRequestTimeout)}' ({options.TotalRequestTimeout.Timeout}) doit être supérieur ou égal à '{nameof(HttpResilienceOptions.AttemptTimeout)}' ({options.AttemptTimeout.Timeout}).");
        }
    }

    private static void ValidateRetry(HttpRetryOptions retry, List<string> failures)
    {
        if (!retry.Enabled)
        {
            return;
        }

        if (retry.MaxRetryAttempts < 1)
        {
            failures.Add($"'{RetryPrefix}.{nameof(HttpRetryOptions.MaxRetryAttempts)}' doit être supérieur ou égal à 1, or il vaut {retry.MaxRetryAttempts}. Pour désactiver le retry, mettez '{RetryPrefix}.{nameof(HttpRetryOptions.Enabled)}' à false.");
        }

        CheckRange(retry.Delay, TimeSpan.Zero, MaxDuration, $"{RetryPrefix}.{nameof(HttpRetryOptions.Delay)}", failures);
    }

    private static void ValidateCircuitBreaker(HttpResilienceOptions options, List<string> failures)
    {
        var circuitBreaker = options.CircuitBreaker;
        if (!circuitBreaker.Enabled)
        {
            return;
        }

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

        if (options.AttemptTimeout.Enabled)
        {
            var minimumSamplingDuration = options.AttemptTimeout.Timeout * 2;
            if (circuitBreaker.SamplingDuration < minimumSamplingDuration)
            {
                failures.Add($"'{CircuitBreakerPrefix}.{nameof(HttpCircuitBreakerOptions.SamplingDuration)}' ({circuitBreaker.SamplingDuration}) doit valoir au moins le double de '{nameof(HttpResilienceOptions.AttemptTimeout)}' ({options.AttemptTimeout.Timeout}), soit {minimumSamplingDuration}, afin que la fenêtre d'observation puisse contenir plusieurs tentatives.");
            }
        }
    }
}
