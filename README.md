# Krosoft.Extensions.Resilience

[![forthebadge](https://forthebadge.com/badges/built-with-love.svg)](https://forthebadge.com) [![forthebadge](https://forthebadge.com/badges/made-with-c-sharp.svg)](https://forthebadge.com)

[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=krosoft-dev_Krosoft.Extensions.Resilience&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=krosoft-dev_Krosoft.Extensions.Resilience)

## Timeout du HttpClient

Lorsque `TotalRequestTimeout` est activé, `AddResilience()` passe `HttpClient.Timeout` à `Timeout.InfiniteTimeSpan` : c'est le pipeline de résilience qui porte le timeout global. Un `client.Timeout` défini dans `AddHttpClient(...)` est alors écrasé. Si `TotalRequestTimeout` est désactivé, le timeout du `HttpClient` (100 s par défaut) est conservé.
