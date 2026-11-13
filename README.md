# Krosoft.Extensions.Resilience

[![forthebadge](https://forthebadge.com/badges/built-with-love.svg)](https://forthebadge.com) [![forthebadge](https://forthebadge.com/badges/made-with-c-sharp.svg)](https://forthebadge.com)

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=krosoft-dev_Krosoft.Extensions.Resilience&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=krosoft-dev_Krosoft.Extensions.Resilience)

## Codes HTTP supplémentaires pour le retry

Par défaut, le retry ne relance que les erreurs transitoires (`HttpClientResiliencePredicates.IsTransient` : 408, 429, 5xx, exceptions réseau et timeouts). `Retry.AdditionalStatusCodes` ajoute des codes à cette liste, sans retirer les codes par défaut. Les codes doivent être compris entre 100 et 599.

À réserver aux clients pour lesquels le code ne peut jamais être une réponse métier (par exemple un 404 renvoyé par un reverse proxy pendant un redéploiement).

```json
{
  "HttpResilience": {
    "Retry": {
      "Enabled": true,
      "MaxRetryAttempts": 3,
      "Delay": "00:00:02"
    },
    "Clients": {
      "traces": {
        "Retry": {
          "AdditionalStatusCodes": [ 404 ]
        }
      }
    }
  }
}
```

```csharp
services.AddHttpClient("traces")
        .AddResilience(options =>
        {
            options.Retry.Enabled = true;
            options.Retry.AdditionalStatusCodes = [404];
        });
```

Les codes de la section commune `HttpResilience:Retry` et ceux de `HttpResilience:Clients:{nom}:Retry` s'additionnent.

## Timeout du HttpClient

Lorsque `TotalRequestTimeout` est activé, `AddResilience()` passe `HttpClient.Timeout` à `Timeout.InfiniteTimeSpan` : c'est le pipeline de résilience qui porte le timeout global. Un `client.Timeout` défini dans `AddHttpClient(...)` est alors écrasé. Si `TotalRequestTimeout` est désactivé, le timeout du `HttpClient` (100 s par défaut) est conservé.
