using Krosoft.Extensions.Resilience.Extensions;
using Krosoft.Extensions.Resilience.Models;
using Krosoft.Extensions.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Krosoft.Extensions.Resilience.Tests.Extensions;

[TestClass]
public class HttpResilienceOptionsBindingTests : BaseTest
{
    private const string ClientSansSurcharge = "client-standard";
    private const string ClientAvecSurcharge = "client-surcharge";

    private static HttpResilienceOptions GetOptions(ServiceProvider provider, string clientName) =>
        provider.GetRequiredService<IOptionsMonitor<HttpResilienceOptions>>().Get(clientName);

    [TestMethod]
    public void Options_SansSurchargeParClient_UtiliseLaSectionCommune()
    {
        using var provider = CreateServiceCollection(services =>
        {
            services.AddHttpClient(ClientSansSurcharge).AddResilience();
        });

        var options = GetOptions(provider, ClientSansSurcharge);

        Check.That(options.AttemptTimeout.Timeout).IsEqualTo(TimeSpan.FromSeconds(5));
        Check.That(options.TotalRequestTimeout.Timeout).IsEqualTo(TimeSpan.FromSeconds(20));
        Check.That(options.Retry.MaxRetryAttempts).IsEqualTo(4);
        Check.That(options.CircuitBreaker.MinimumThroughput).IsEqualTo(20);
    }

    [TestMethod]
    public void Options_AvecSurchargeParClient_LaSurchargeGagneEtLeResteEstHerite()
    {
        using var provider = CreateServiceCollection(services =>
        {
            services.AddHttpClient(ClientAvecSurcharge).AddResilience();
        });

        var options = GetOptions(provider, ClientAvecSurcharge);

        Check.That(options.Retry.MaxRetryAttempts).IsEqualTo(7);
        Check.That(options.AttemptTimeout.Timeout).IsEqualTo(TimeSpan.FromSeconds(5));
        Check.That(options.CircuitBreaker.MinimumThroughput).IsEqualTo(20);
    }

    [TestMethod]
    public void Options_DeuxClientsDifferents_SontConfiguresIndependamment()
    {
        using var provider = CreateServiceCollection(services =>
        {
            services.AddHttpClient(ClientSansSurcharge).AddResilience();
            services.AddHttpClient(ClientAvecSurcharge).AddResilience();
        });

        Check.That(GetOptions(provider, ClientSansSurcharge).Retry.MaxRetryAttempts).IsEqualTo(4);
        Check.That(GetOptions(provider, ClientAvecSurcharge).Retry.MaxRetryAttempts).IsEqualTo(7);
    }

    [TestMethod]
    public void Options_ConfigureParCode_EcraseLaConfiguration()
    {
        using var provider = CreateServiceCollection(services =>
        {
            services.AddHttpClient(ClientSansSurcharge)
                    .AddResilience(options => options.Retry.MaxRetryAttempts = 1);
        });

        var options = GetOptions(provider, ClientSansSurcharge);

        Check.That(options.Retry.MaxRetryAttempts).IsEqualTo(1);
        Check.That(options.AttemptTimeout.Timeout).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public void Options_SectionDeConfigurationAbsente_UtiliseLesValeursParDefaut()
    {
        using var provider = CreateServiceCollection(services =>
        {
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddHttpClient(ClientSansSurcharge).AddResilience();
        });

        var options = GetOptions(provider, ClientSansSurcharge);

        Check.That(options.TotalRequestTimeout.Enabled).IsTrue();
        Check.That(options.TotalRequestTimeout.Timeout).IsEqualTo(TimeSpan.FromSeconds(30));
        Check.That(options.AttemptTimeout.Enabled).IsTrue();
        Check.That(options.AttemptTimeout.Timeout).IsEqualTo(TimeSpan.FromSeconds(10));
        Check.That(options.Retry.Enabled).IsFalse();
        Check.That(options.Retry.MaxRetryAttempts).IsEqualTo(3);
        Check.That(options.Retry.Delay).IsEqualTo(TimeSpan.FromSeconds(2));
        Check.That(options.CircuitBreaker.Enabled).IsTrue();
        Check.That(options.CircuitBreaker.FailureRatio).IsEqualTo(0.5);
        Check.That(options.CircuitBreaker.MinimumThroughput).IsEqualTo(10);
        Check.That(options.CircuitBreaker.SamplingDuration).IsEqualTo(TimeSpan.FromSeconds(30));
        Check.That(options.CircuitBreaker.BreakDuration).IsEqualTo(TimeSpan.FromSeconds(15));
    }

    [TestMethod]
    public void CreateClient_SectionDeConfigurationAbsente_NeLevePasDException()
    {
        using var provider = CreateServiceCollection(services =>
        {
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddHttpClient(ClientSansSurcharge).AddResilience();
        });

        var httpClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientSansSurcharge);

        Check.That(httpClient).IsNotNull();
    }

    [TestMethod]
    public void AddResilience_DeuxFoisSurLeMemeClient_LeveUneErreurExplicite()
    {
        Check.ThatCode(() => CreateServiceCollection(services =>
             {
                 var httpClientBuilder = services.AddHttpClient(ClientSansSurcharge);
                 httpClientBuilder.AddResilience();
                 httpClientBuilder.AddResilience();
             }))
             .Throws<InvalidOperationException>()
             .WhichMember(exception => exception.Message)
             .Contains(ClientSansSurcharge, "AddResilience");
    }

    [TestMethod]
    public void CreateClient_ConfigurationIncoherente_LeveUneErreurExplicite()
    {
        using var provider = CreateServiceCollection(services =>
        {
            services.AddHttpClient(ClientSansSurcharge)
                    .AddResilience(options =>
                    {
                        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(5);
                    });
        });

        Check.ThatCode(() => provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientSansSurcharge))
             .Throws<OptionsValidationException>()
             .WhichMember(exception => exception.Message)
             .Contains(ClientSansSurcharge, nameof(HttpCircuitBreakerOptions.SamplingDuration), nameof(HttpResilienceOptions.AttemptTimeout));
    }
}
