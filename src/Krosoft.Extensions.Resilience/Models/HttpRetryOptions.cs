namespace Krosoft.Extensions.Resilience.Models;

public record HttpRetryOptions
{
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);
    public bool UseJitter { get; set; } = true;
}
