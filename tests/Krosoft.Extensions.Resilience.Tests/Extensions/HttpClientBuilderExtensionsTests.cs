using System.Net;
using Krosoft.Extensions.Resilience.Extensions;
using Krosoft.Extensions.Resilience.Models;
using Krosoft.Extensions.Resilience.Tests.Core;
using Krosoft.Extensions.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Krosoft.Extensions.Resilience.Tests.Extensions;

[TestClass]
public class HttpClientBuilderExtensionsTests : BaseTest
{
    private const string ClientName = "client-resilience";
    private const string RequestUri = "/todos/1";

    // Timeouts et circuit breaker activés ; le retry reste opt-in, activé par test via refine.
    private static Action<HttpResilienceOptions> Fast(Action<HttpResilienceOptions>? refine = null) =>
        options =>
        {
            options.TotalRequestTimeout.Enabled = true;
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.AttemptTimeout.Enabled = true;
            options.AttemptTimeout.Timeout = TimeSpan.FromMilliseconds(200);
            options.Retry.Enabled = false;
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.Zero;
            options.CircuitBreaker.Enabled = true;
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.MinimumThroughput = 100;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(2);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromMilliseconds(500);

            refine?.Invoke(options);
        };

    private ServiceProvider CreateProvider(MockHttpMessageHandler handler,
                                           Action<HttpResilienceOptions> configure,
                                           ILoggerProvider? loggerProvider = null) =>
        CreateServiceCollection(services =>
        {
            if (loggerProvider != null)
            {
                services.AddLogging(builder => builder.AddProvider(loggerProvider).SetMinimumLevel(LogLevel.Trace));
            }

            var httpClientBuilder = services.AddHttpClient(ClientName, client => client.BaseAddress = new Uri("https://krosoft.local/"));
            httpClientBuilder.AddResilience(configure);
            httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => handler);
        });

    private static HttpClient GetHttpClient(ServiceProvider provider) =>
        provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);

    [TestMethod]
    public async Task RetryActif_ErreurServeur_RejoueLeNombreDeTentativesConfigure()
    {
        var handler = MockHttpMessageHandler.Always(HttpStatusCode.InternalServerError);
        await using var provider = CreateProvider(handler, Fast(options =>
        {
            options.Retry.Enabled = true;
            options.Retry.MaxRetryAttempts = 2;
        }));

        var response = await GetHttpClient(provider).GetAsync(RequestUri, CancellationToken.None);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
        Check.That(handler.CallCount).IsEqualTo(3);
    }

    [TestMethod]
    public async Task RetryActif_ErreurServeurPuisSucces_RetourneLaReponseDeLaDerniereTentative()
    {
        var handler = MockHttpMessageHandler.From(attempt => attempt < 3 ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
        await using var provider = CreateProvider(handler, Fast(options =>
        {
            options.Retry.Enabled = true;
            options.Retry.MaxRetryAttempts = 2;
        }));

        var response = await GetHttpClient(provider).GetAsync(RequestUri, CancellationToken.None);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(handler.CallCount).IsEqualTo(3);
    }

    [TestMethod]
    public async Task RetryActif_ReponseValide_NeRejouePas()
    {
        var handler = MockHttpMessageHandler.Always(HttpStatusCode.OK);
        await using var provider = CreateProvider(handler, Fast(options => options.Retry.Enabled = true));

        var response = await GetHttpClient(provider).GetAsync(RequestUri, CancellationToken.None);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(handler.CallCount).IsEqualTo(1);
    }

    [TestMethod]
    public async Task RetryInactif_ErreurServeur_NeRejouePas()
    {
        var handler = MockHttpMessageHandler.Always(HttpStatusCode.InternalServerError);
        await using var provider = CreateProvider(handler, Fast());

        var response = await GetHttpClient(provider).GetAsync(RequestUri, CancellationToken.None);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
        Check.That(handler.CallCount).IsEqualTo(1);
    }

    [TestMethod]
    public async Task RetryActif_ServeurQuiPend_AppliqueLeTimeoutParTentativeEtRejoue()
    {
        var handler = MockHttpMessageHandler.Delayed(TimeSpan.FromSeconds(5));
        await using var provider = CreateProvider(handler, Fast(options =>
        {
            options.Retry.Enabled = true;
            options.Retry.MaxRetryAttempts = 1;
        }));
        var httpClient = GetHttpClient(provider);

        Check.ThatCode(() => httpClient.GetAsync(RequestUri, CancellationToken.None))
             .Throws<TimeoutRejectedException>();

        Check.That(handler.CallCount).IsEqualTo(2);
    }

    [TestMethod]
    public async Task RetryInactif_ServeurQuiPend_AppliqueLeTimeoutParTentativeSansRejouer()
    {
        var handler = MockHttpMessageHandler.Delayed(TimeSpan.FromSeconds(5));
        await using var provider = CreateProvider(handler, Fast());
        var httpClient = GetHttpClient(provider);

        Check.ThatCode(() => httpClient.GetAsync(RequestUri, CancellationToken.None))
             .Throws<TimeoutRejectedException>();

        Check.That(handler.CallCount).IsEqualTo(1);
    }

    [TestMethod]
    public async Task TotalRequestTimeoutDesactive_ServeurQuiPend_AppliqueLAttemptTimeout()
    {
        var handler = MockHttpMessageHandler.Delayed(TimeSpan.FromSeconds(5));
        await using var provider = CreateProvider(handler, Fast(options => options.TotalRequestTimeout.Enabled = false));
        var httpClient = GetHttpClient(provider);

        Check.ThatCode(() => httpClient.GetAsync(RequestUri, CancellationToken.None))
             .Throws<TimeoutRejectedException>();

        Check.That(handler.CallCount).IsEqualTo(1);
    }

    [TestMethod]
    public async Task RetryActif_ErreurServeur_JournaliseViaLaTelemetrieDuPipeline()
    {
        var loggerProvider = new TestLoggerProvider();
        var handler = MockHttpMessageHandler.Always(HttpStatusCode.InternalServerError);
        await using var provider = CreateProvider(handler, Fast(options =>
        {
            options.Retry.Enabled = true;
            options.Retry.MaxRetryAttempts = 1;
        }), loggerProvider);

        await GetHttpClient(provider).GetAsync(RequestUri, CancellationToken.None);

        var messages = loggerProvider.Entries
                                     .Where(entry => entry.Category.StartsWith("Polly", StringComparison.Ordinal))
                                     .Select(entry => entry.Message)
                                     .ToList();

        Check.That(messages).Not.IsEmpty();
        Check.That(string.Join(Environment.NewLine, messages)).Contains("Retry");
    }

    [TestMethod]
    public async Task CircuitBreakerActif_SeuilDEchecsAtteint_OuvreLeCircuitPuisLeRefermeApresBreakDuration()
    {
        var isDown = true;
        var handler = MockHttpMessageHandler.From(_ => isDown ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
        await using var provider = CreateProvider(handler, Fast(options =>
        {
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.FailureRatio = 0.5;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(2);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromMilliseconds(500);
        }));
        var httpClient = GetHttpClient(provider);

        await httpClient.GetAsync(RequestUri, CancellationToken.None);
        await httpClient.GetAsync(RequestUri, CancellationToken.None);
        Check.That(handler.CallCount).IsEqualTo(2);

        // Circuit ouvert : plus rien ne part vers le serveur.
        Check.ThatCode(() => httpClient.GetAsync(RequestUri, CancellationToken.None))
             .Throws<BrokenCircuitException>();
        Check.That(handler.CallCount).IsEqualTo(2);

        // Passée la BreakDuration, le circuit devient semi-ouvert : un appel de test passe et le referme.
        isDown = false;
        await Task.Delay(TimeSpan.FromMilliseconds(700), CancellationToken.None);

        var response = await httpClient.GetAsync(RequestUri, CancellationToken.None);

        Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        Check.That(handler.CallCount).IsEqualTo(3);
    }

    [TestMethod]
    public async Task CircuitBreakerDesactive_EchecsRepetes_NOuvreJamaisLeCircuit()
    {
        var handler = MockHttpMessageHandler.Always(HttpStatusCode.InternalServerError);
        await using var provider = CreateProvider(handler, Fast(options =>
        {
            options.CircuitBreaker.Enabled = false;
            options.CircuitBreaker.MinimumThroughput = 2;
        }));
        var httpClient = GetHttpClient(provider);

        for (var i = 0; i < 5; i++)
        {
            var response = await httpClient.GetAsync(RequestUri, CancellationToken.None);
            Check.That(response.StatusCode).IsEqualTo(HttpStatusCode.InternalServerError);
        }

        Check.That(handler.CallCount).IsEqualTo(5);
    }
}