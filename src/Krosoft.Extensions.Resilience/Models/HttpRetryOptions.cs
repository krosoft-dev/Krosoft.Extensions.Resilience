namespace Krosoft.Extensions.Resilience.Models;

public record HttpRetryOptions
{
    // Opt-in : le retry rejoue aussi les requêtes non idempotentes (POST), à n'activer qu'en connaissance de cause.
    public bool Enabled { get; set; }

    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);
}
