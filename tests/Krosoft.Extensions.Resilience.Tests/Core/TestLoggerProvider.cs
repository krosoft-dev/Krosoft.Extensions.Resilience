using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Krosoft.Extensions.Resilience.Tests.Core;

internal sealed class TestLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<(string Category, string Message)> _entries = new();

    public IEnumerable<(string Category, string Message)> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new TestLogger(this, categoryName);

    public void Dispose()
    {
    }

    internal void Add(string category, string message) => _entries.Enqueue((category, message));
}
