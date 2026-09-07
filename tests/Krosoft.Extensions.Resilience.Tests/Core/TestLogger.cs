using Microsoft.Extensions.Logging;

namespace Krosoft.Extensions.Resilience.Tests.Core;

internal sealed class TestLogger : ILogger
{
    private readonly string _categoryName;
    private readonly TestLoggerProvider _provider;

    public TestLogger(TestLoggerProvider provider, string categoryName)
    {
        _provider = provider;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel,
                            EventId eventId,
                            TState state,
                            Exception? exception,
                            Func<TState, Exception?, string> formatter)
    {
        _provider.Add(_categoryName, formatter(state, exception));
    }
}
