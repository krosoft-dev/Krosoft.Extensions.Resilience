namespace Krosoft.Extensions.Resilience.Models;

public record HttpResilienceOptions
{
    public const string SectionName = "HttpResilience";
    public const string ClientsSectionName = "Clients";

    public HttpTimeoutOptions TotalRequestTimeout { get; set; } = new() { Timeout = TimeSpan.FromSeconds(30) };
    public HttpTimeoutOptions AttemptTimeout { get; set; } = new() { Timeout = TimeSpan.FromSeconds(10) };
    public HttpRetryOptions Retry { get; set; } = new();
    public HttpCircuitBreakerOptions CircuitBreaker { get; set; } = new();
}
