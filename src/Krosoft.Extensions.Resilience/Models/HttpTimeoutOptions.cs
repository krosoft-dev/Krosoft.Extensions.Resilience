namespace Krosoft.Extensions.Resilience.Models;

public record HttpTimeoutOptions
{
    // Désactiver les deux timeouts (Attempt + Total) laisse une requête qui pend
    // bornée uniquement par HttpClient.Timeout (100 s par défaut) : à éviter en production.
    public bool Enabled { get; set; } = true;

    public TimeSpan Timeout { get; set; }
}
