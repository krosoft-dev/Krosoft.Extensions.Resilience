using Krosoft.Extensions.Resilience.Models;
using Krosoft.Extensions.Resilience.Validations;
using Krosoft.Extensions.Testing;

namespace Krosoft.Extensions.Resilience.Tests.Validations;

[TestClass]
public class HttpResilienceOptionsValidatorTests : BaseTest
{
    private const string ClientName = "mon-client";

    private readonly HttpResilienceOptionsValidator _validator = new();

    [TestMethod]
    public void Validate_OptionsParDefaut_EstValide()
    {
        var result = _validator.Validate(ClientName, new HttpResilienceOptions());

        Check.That(result.Succeeded).IsTrue();
    }

    [TestMethod]
    public void Validate_TousLesBlocsDesactives_Echoue()
    {
        var options = new HttpResilienceOptions();
        options.TotalRequestTimeout.Enabled = false;
        options.AttemptTimeout.Enabled = false;
        options.CircuitBreaker.Enabled = false;

        var result = _validator.Validate(ClientName, options);

        Check.That(result.Failed).IsTrue();
        Check.That(result.FailureMessage).Contains("pipeline vide");
    }

    [TestMethod]
    public void Validate_TotalRequestTimeoutInferieurAuAttemptTimeout_Echoue()
    {
        var options = new HttpResilienceOptions
        {
            AttemptTimeout = new HttpTimeoutOptions { Timeout = TimeSpan.FromSeconds(30) },
            TotalRequestTimeout = new HttpTimeoutOptions { Timeout = TimeSpan.FromSeconds(10) }
        };
        options.CircuitBreaker.Enabled = false;

        var result = _validator.Validate(ClientName, options);

        Check.That(result.Failed).IsTrue();
        Check.That(result.FailureMessage).Contains(ClientName,
                                                   nameof(HttpResilienceOptions.TotalRequestTimeout),
                                                   nameof(HttpResilienceOptions.AttemptTimeout));
    }

    [TestMethod]
    public void Validate_SamplingDurationInferieureAuDoubleDuAttemptTimeout_Echoue()
    {
        var options = new HttpResilienceOptions
        {
            AttemptTimeout = new HttpTimeoutOptions { Timeout = TimeSpan.FromSeconds(10) }
        };
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(5);

        var result = _validator.Validate(ClientName, options);

        Check.That(result.Failed).IsTrue();
        Check.That(result.FailureMessage).Contains(nameof(HttpCircuitBreakerOptions.SamplingDuration), "double");
    }

    [TestMethod]
    public void Validate_AttemptTimeoutDesactive_IgnoreLaContrainteSurSamplingDuration()
    {
        var options = new HttpResilienceOptions();
        options.AttemptTimeout.Enabled = false;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(1);

        var result = _validator.Validate(ClientName, options);

        Check.That(result.Succeeded).IsTrue();
    }

    [TestMethod]
    public void Validate_AttemptTimeoutNul_Echoue()
    {
        var options = new HttpResilienceOptions
        {
            AttemptTimeout = new HttpTimeoutOptions { Timeout = TimeSpan.Zero }
        };

        var result = _validator.Validate(ClientName, options);

        Check.That(result.Failed).IsTrue();
        Check.That(result.FailureMessage).Contains(nameof(HttpResilienceOptions.AttemptTimeout));
    }

    [TestMethod]
    public void Validate_RetryDesactive_IgnoreSesSeuils()
    {
        var options = new HttpResilienceOptions();
        options.Retry.Enabled = false;
        options.Retry.MaxRetryAttempts = 0;

        var result = _validator.Validate(ClientName, options);

        Check.That(result.Succeeded).IsTrue();
    }

    [TestMethod]
    public void Validate_RetryActifMaxRetryAttemptsNul_EchoueEtOrienteVersEnabled()
    {
        var options = new HttpResilienceOptions();
        options.Retry.Enabled = true;
        options.Retry.MaxRetryAttempts = 0;

        var result = _validator.Validate(ClientName, options);

        Check.That(result.Failed).IsTrue();
        Check.That(result.FailureMessage).Contains(nameof(HttpRetryOptions.MaxRetryAttempts), nameof(HttpRetryOptions.Enabled));
    }

    [TestMethod]
    public void Validate_CircuitBreakerDesactive_IgnoreSesSeuils()
    {
        var options = new HttpResilienceOptions();
        options.CircuitBreaker.Enabled = false;
        options.CircuitBreaker.MinimumThroughput = 0;
        options.CircuitBreaker.FailureRatio = 5;

        var result = _validator.Validate(ClientName, options);

        Check.That(result.Succeeded).IsTrue();
    }

    [TestMethod]
    public void Validate_MinimumThroughputInferieurADeux_Echoue()
    {
        var options = new HttpResilienceOptions();
        options.CircuitBreaker.MinimumThroughput = 1;

        var result = _validator.Validate(ClientName, options);

        Check.That(result.Failed).IsTrue();
        Check.That(result.FailureMessage).Contains(nameof(HttpCircuitBreakerOptions.MinimumThroughput));
    }

    [TestMethod]
    public void Validate_FailureRatioHorsBornes_Echoue()
    {
        var options = new HttpResilienceOptions();
        options.CircuitBreaker.FailureRatio = 1.5;

        var result = _validator.Validate(ClientName, options);

        Check.That(result.Failed).IsTrue();
        Check.That(result.FailureMessage).Contains(nameof(HttpCircuitBreakerOptions.FailureRatio));
    }

    [TestMethod]
    public void Validate_BreakDurationTropCourte_Echoue()
    {
        var options = new HttpResilienceOptions();
        options.CircuitBreaker.BreakDuration = TimeSpan.FromMilliseconds(100);

        var result = _validator.Validate(ClientName, options);

        Check.That(result.Failed).IsTrue();
        Check.That(result.FailureMessage).Contains(nameof(HttpCircuitBreakerOptions.BreakDuration));
    }

    [TestMethod]
    public void Validate_PlusieursErreurs_LesRemonteToutes()
    {
        var options = new HttpResilienceOptions
        {
            AttemptTimeout = new HttpTimeoutOptions { Timeout = TimeSpan.Zero }
        };
        options.Retry.Enabled = true;
        options.Retry.MaxRetryAttempts = 0;
        options.CircuitBreaker.MinimumThroughput = 0;

        var result = _validator.Validate(ClientName, options);

        Check.That(result.Failed).IsTrue();
        Check.That(result.Failures).HasSize(3);
    }

    [TestMethod]
    public void Validate_SansNomDeClient_MentionneLaSectionRacine()
    {
        var options = new HttpResilienceOptions
        {
            AttemptTimeout = new HttpTimeoutOptions { Timeout = TimeSpan.Zero }
        };

        var result = _validator.Validate(null, options);

        Check.That(result.Failed).IsTrue();
        Check.That(result.FailureMessage).Contains(HttpResilienceOptions.SectionName);
    }
}
