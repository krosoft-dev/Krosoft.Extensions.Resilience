namespace Krosoft.Extensions.Resilience.Models;

public record HttpRetryOptions
{
    public bool Enabled { get; set; }

    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);
    public int[] AdditionalStatusCodes { get; set; } = [];
}
