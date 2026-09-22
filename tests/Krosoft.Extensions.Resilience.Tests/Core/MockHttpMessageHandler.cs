using System.Net;

namespace Krosoft.Extensions.Resilience.Tests.Core;

public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<int, CancellationToken, Task<HttpResponseMessage>> _responder;
    private int _callCount;

    private MockHttpMessageHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public int CallCount => Volatile.Read(ref _callCount);

    public static MockHttpMessageHandler Always(HttpStatusCode statusCode) => From(_ => statusCode);

    public static MockHttpMessageHandler Delayed(TimeSpan delay, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(async (_, cancellationToken) =>
        {
            await Task.Delay(delay, cancellationToken);

            return new HttpResponseMessage(statusCode);
        });

    public static MockHttpMessageHandler From(Func<int, HttpStatusCode> statusCodeProvider) =>
        new((attempt, _) => Task.FromResult(new HttpResponseMessage(statusCodeProvider(attempt))));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                           CancellationToken cancellationToken)
    {
        var attempt = Interlocked.Increment(ref _callCount);

        return _responder(attempt, cancellationToken);
    }
}
