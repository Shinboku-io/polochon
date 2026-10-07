# Polochon.Telemetry.AzureMonitor

Adds [Azure Monitor](https://learn.microsoft.com/azure/azure-monitor/app/opentelemetry-overview) (Application Insights) as an exporter of a Polochon host's OpenTelemetry pipeline, set up by `Polochon.Telemetry.OpenTelemetry`.

It's configured once, on the host. Modules never see the connection string or the credential.

## Install

```sh
dotnet add package Polochon.Telemetry.AzureMonitor
```

## Quick start

```csharp
using Polochon;
using Polochon.Telemetry.AzureMonitor;
using Polochon.Telemetry.OpenTelemetry;

builder.Services.AddPolochon()
    .WithOpenTelemetry(telemetry => telemetry.ServiceName = builder.Environment.ApplicationName)
    .WithAzureMonitor();

builder.Services.AddInventory().WithOpenTelemetry(); // the module's logs too
```

With no configuration, the connection string comes from the standard `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable, which App Service and Container Apps can set for you. Call `WithAzureMonitor()` after `WithOpenTelemetry()`; calling it first throws.

## Samples

### Connection string from configuration

```csharp
.WithAzureMonitor(azure => azure.ConnectionString = builder.Configuration.GetConnectionString("ApplicationInsights"));
```

### Microsoft Entra authentication instead of the instrumentation key

```csharp
.WithAzureMonitor(azure =>
{
    azure.ConnectionString = builder.Configuration.GetConnectionString("ApplicationInsights");
    azure.Credential = new DefaultAzureCredential();
});
```

Grant the identity the **Monitoring Metrics Publisher** role on the Application Insights resource. The connection string still identifies the resource; the credential authenticates ingestion.

### Keep sending to Azure Monitor and to an OTLP collector

The exporters simply add up:

```csharp
builder.Services.AddPolochon()
    .WithOpenTelemetry(telemetry => telemetry.OtlpEndpoint = new Uri("http://otel-collector:4317"))
    .WithAzureMonitor();
```

## How it fits the pipeline

- **Only the signals the host collects:** the Azure Monitor exporter is added to traces always, and to metrics and logs only when `WithOpenTelemetry()` collects them. Setting `EnableLogging = false` keeps logs out of Application Insights too. This is why the package uses the per-signal exporters rather than `UseAzureMonitorExporter()`, which switches every signal on.
- **Sampling stays the host's:** Azure Monitor's trace exporter installs its own sampler, rate-limited by default, replacing the pipeline's. `WithAzureMonitor()` therefore sets it to the host's `SamplingRate`, and Application Insights records that rate to extrapolate counts. To use rate-limited sampling instead, set `azure.TracesPerSecond`.
- **Offline storage:** telemetry that can't be sent right away is kept on disk and retried, as Azure Monitor does by default. `azure.StorageDirectory` and `azure.DisableOfflineStorage` control it.
- **Not enabled:** Live Metrics and Application Insights' standard metrics are features of `UseAzureMonitorExporter()`, which this package doesn't use.
