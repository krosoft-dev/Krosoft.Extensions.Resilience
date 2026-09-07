using Krosoft.Extensions.Resilience.Models;
using Krosoft.Extensions.Resilience.Validations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Krosoft.Extensions.Resilience.Extensions;

public static class HttpClientBuilderExtensions
{
    public static IHttpClientBuilder AddResilienceHandlerWithRetry(this IHttpClientBuilder httpClientBuilder,
                                                                   Action<HttpResilienceOptions>? configure = null) =>
        httpClientBuilder.AddResilienceHandler(true, configure);

    public static IHttpClientBuilder AddResilienceHandlerWithoutRetry(this IHttpClientBuilder httpClientBuilder,
                                                                      Action<HttpResilienceOptions>? configure = null) =>
        httpClientBuilder.AddResilienceHandler(false, configure);

    private static IHttpClientBuilder AddResilienceHandler(this IHttpClientBuilder httpClientBuilder,
                                                           bool withRetry,
                                                           Action<HttpResilienceOptions>? configure)
    {
        var clientName = httpClientBuilder.Name;

        EnsureNotAlreadyRegistered(httpClientBuilder.Services, clientName);

        httpClientBuilder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<HttpResilienceOptions>, HttpResilienceOptionsValidator>());

        var optionsBuilder = httpClientBuilder.Services
                                              .AddOptions<HttpResilienceOptions>(clientName)
                                              .Configure<IConfiguration>((options, configuration) => Bind(options, configuration, clientName))
                                              .ValidateOnStart();

        if (configure != null)
        {
            optionsBuilder.Configure(configure);
        }

        httpClientBuilder.AddResilienceHandler($"{clientName}-krosoft", (pipelineBuilder, context) =>
        {
            context.EnableReloads<HttpResilienceOptions>(clientName);
            var options = context.GetOptions<HttpResilienceOptions>(clientName);

            pipelineBuilder.AddTimeout(new HttpTimeoutStrategyOptions
            {
                Name = "TotalRequestTimeout",
                Timeout = options.TotalRequestTimeout
            });

            if (withRetry)
            {
                pipelineBuilder.AddRetry(new HttpRetryStrategyOptions
                {
                    Name = "Retry",
                    MaxRetryAttempts = options.Retry.MaxRetryAttempts,
                    Delay = options.Retry.Delay,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = options.Retry.UseJitter
                });
            }

            pipelineBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                Name = "CircuitBreaker",
                FailureRatio = options.CircuitBreaker.FailureRatio,
                MinimumThroughput = options.CircuitBreaker.MinimumThroughput,
                SamplingDuration = options.CircuitBreaker.SamplingDuration,
                BreakDuration = options.CircuitBreaker.BreakDuration
            });

            pipelineBuilder.AddTimeout(new HttpTimeoutStrategyOptions
            {
                Name = "AttemptTimeout",
                Timeout = options.AttemptTimeout
            });
        });

        return httpClientBuilder;
    }

    private static void EnsureNotAlreadyRegistered(IServiceCollection services, string clientName)
    {
        var marker = new ResilienceHandlerMarker(clientName);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(ResilienceHandlerMarker)
                                       && marker.Equals(descriptor.ImplementationInstance)))
        {
            throw new InvalidOperationException($"Un pipeline de résilience est déjà enregistré pour le client HTTP '{clientName}'. Utilisez soit {nameof(AddResilienceHandlerWithRetry)}, soit {nameof(AddResilienceHandlerWithoutRetry)}, mais pas les deux.");
        }

        services.AddSingleton(marker);
    }

    private static void Bind(HttpResilienceOptions options, IConfiguration configuration, string clientName)
    {
        var section = configuration.GetSection(HttpResilienceOptions.SectionName);
        section.Bind(options);
        section.GetSection($"{HttpResilienceOptions.ClientsSectionName}:{clientName}").Bind(options);
    }
}
