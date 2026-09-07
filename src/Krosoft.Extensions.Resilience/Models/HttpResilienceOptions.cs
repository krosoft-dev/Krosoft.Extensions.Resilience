namespace Krosoft.Extensions.Resilience.Models;

public record HttpResilienceOptions
{
    public const string SectionName = "HttpResilience";
    public const string ClientsSectionName = "Clients";

    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan TotalRequestTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public HttpRetryOptions Retry { get; set; } = new();
    public HttpCircuitBreakerOptions CircuitBreaker { get; set; } = new();
}
