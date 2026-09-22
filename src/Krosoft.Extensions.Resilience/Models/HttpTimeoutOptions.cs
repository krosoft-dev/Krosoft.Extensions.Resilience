namespace Krosoft.Extensions.Resilience.Models;

public record HttpTimeoutOptions
{
   public bool Enabled { get; set; }

    public TimeSpan Timeout { get; set; }
}
