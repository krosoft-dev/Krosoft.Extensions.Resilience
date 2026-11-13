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
    public void Options_CodesSupplementairesDansLaSurchargeParClient_SontLies()
    {
        const string clientName = "client-retry-404";
        using var provider = CreateServiceCollection(services =>
        {
            services.AddHttpClient(ClientSansSurcharge).AddResilience();
            services.AddHttpClient(clientName).AddResilience();
        });

        Check.That(GetOptions(provider, clientName).Retry.AdditionalStatusCodes).ContainsExactly(404);
        Check.That(GetOptions(provider, ClientSansSurcharge).Retry.AdditionalStatusCodes).IsEmpty();
    }

    [TestMethod]
    public void Options_CodesSupplementairesCommunsEtParClient_SAdditionnent()
    {
        var configuration = new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                ["HttpResilience:Retry:Enabled"] = "true",
                                ["HttpResilience:Retry:AdditionalStatusCodes:0"] = "502",
                                [$"HttpResilience:Clients:{ClientSansSurcharge}:Retry:AdditionalStatusCodes:0"] = "404"
                            })
                            .Build();
        using var provider = CreateServiceCollection(services =>
        {
            services.AddSingleton<IConfiguration>(configuration);
            services.AddHttpClient(ClientSansSurcharge).AddResilience();
        });

        Check.That(GetOptions(provider, ClientSansSurcharge).Retry.AdditionalStatusCodes).ContainsExactly(502, 404);
    }

    [TestMethod]
    public void Options_CodesSupplementairesParCode_SontLies()
    {
        using var provider = CreateServiceCollection(services =>
        {
            services.AddHttpClient(ClientSansSurcharge)
                    .AddResilience(options => options.Retry.AdditionalStatusCodes = [404, 409]);
        });

        Check.That(GetOptions(provider, ClientSansSurcharge).Retry.AdditionalStatusCodes).ContainsExactly(404, 409);
    }

    [TestMethod]
    public void ValeursParDefaut_ToutesLesStrategiesSontDesactivees()
    {
        var options = new HttpResilienceOptions();

        Check.That(options.TotalRequestTimeout.Enabled).IsFalse();
        Check.That(options.TotalRequestTimeout.Timeout).IsEqualTo(TimeSpan.FromSeconds(30));
        Check.That(options.AttemptTimeout.Enabled).IsFalse();
        Check.That(options.AttemptTimeout.Timeout).IsEqualTo(TimeSpan.FromSeconds(10));
        Check.That(options.Retry.Enabled).IsFalse();
        Check.That(options.Retry.MaxRetryAttempts).IsEqualTo(3);
        Check.That(options.Retry.Delay).IsEqualTo(TimeSpan.FromSeconds(2));
        Check.That(options.Retry.AdditionalStatusCodes).IsEmpty();
        Check.That(options.CircuitBreaker.Enabled).IsFalse();
        Check.That(options.CircuitBreaker.FailureRatio).IsEqualTo(0.5);
        Check.That(options.CircuitBreaker.MinimumThroughput).IsEqualTo(10);
        Check.That(options.CircuitBreaker.SamplingDuration).IsEqualTo(TimeSpan.FromSeconds(30));
        Check.That(options.CircuitBreaker.BreakDuration).IsEqualTo(TimeSpan.FromSeconds(15));
    }

    [TestMethod]
    public void CreateClient_SansConfiguration_LeveErreurPipelineVide()
    {
        using var provider = CreateServiceCollection(services =>
        {
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddHttpClient(ClientSansSurcharge).AddResilience();
        });

        Check.ThatCode(() => provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientSansSurcharge))
             .Throws<OptionsValidationException>()
             .WhichMember(exception => exception.Message)
             .Contains(ClientSansSurcharge, "pipeline vide");
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
                        options.AttemptTimeout.Enabled = true;
                        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                        options.CircuitBreaker.Enabled = true;
                        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(5);
                    });
        });

        Check.ThatCode(() => provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientSansSurcharge))
             .Throws<OptionsValidationException>()
             .WhichMember(exception => exception.Message)
             .Contains(ClientSansSurcharge, nameof(HttpCircuitBreakerOptions.SamplingDuration), nameof(HttpResilienceOptions.AttemptTimeout));
    }
}
