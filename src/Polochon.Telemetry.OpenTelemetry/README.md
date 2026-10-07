# Polochon.Telemetry.OpenTelemetry

Exports a Polochon host's traces, metrics and logs through [OpenTelemetry](https://opentelemetry.io/docs/languages/dotnet/). That covers every module's command and query traces, its dispatch metrics and anything it emits through `IModuleTelemetry`, plus the host's own requests, HTTP calls and runtime metrics.

It's configured once, on the host. Modules keep emitting through the standard .NET APIs and never see an exporter, endpoint or connection string.

```text
module ActivitySource / Meter ("Polochon.Modules.{module}") --\
host ActivitySources / Meters (ASP.NET Core, HttpClient...) ---+--> host OpenTelemetry pipeline --> OTLP / console / Azure Monitor
module ILogger --(module-level WithOpenTelemetry())------------/
```

## Install

```sh
dotnet add package Polochon.Telemetry.OpenTelemetry
```

## Quick start

```csharp
using Polochon;
using Polochon.Telemetry.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPolochon()
    .WithOpenTelemetry(telemetry => telemetry.ServiceName = builder.Environment.ApplicationName);

builder.Services.AddInventory().WithOpenTelemetry();
```

- **Traces and metrics:** the host-level `WithOpenTelemetry()` collects every module's (`Polochon.Modules.*`) with no module-level call.
- **Logs:** each module has its own logger factory. The module-level `WithOpenTelemetry()` sends the module's logs through the host's pipeline, each entry tagged `polochon.module`. Without it, the host exports only its own logs.
- **Export:** nothing leaves the process until an exporter is configured. See the samples below.

## Samples

### See everything locally in the .NET Aspire dashboard

```sh
docker run --rm -it -p 18888:18888 -p 4317:18889 mcr.microsoft.com/dotnet/aspire-dashboard:latest
```

Then run the app with the standard variable set, for example in `launchSettings.json`:

```json
"environmentVariables": {
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4317"
}
```

When `OTEL_EXPORTER_OTLP_ENDPOINT` is set, OTLP export turns on for every signal. It's then fully configured by the standard `OTEL_EXPORTER_OTLP_*` variables (protocol, headers...). Under .NET Aspire orchestration these are set for you.

### Send to a collector at a fixed endpoint

```csharp
builder.Services.AddPolochon().WithOpenTelemetry(telemetry =>
{
    telemetry.ServiceName = "school-inventory";
    telemetry.OtlpEndpoint = new Uri("http://otel-collector:4318");
    telemetry.OtlpProtocol = OtlpExportProtocol.HttpProtobuf;
});
```

### Print to the console while developing

```csharp
telemetry.EnableConsoleExporter = builder.Environment.IsDevelopment();
```

### Sample traces

```csharp
telemetry.SamplingRate = 0.1; // keep 10% of the traces started here
```

A trace that started upstream keeps its caller's sampling decision, so a request is never half-traced.

### Module logs with Serilog

A module can log through Serilog and still export through OpenTelemetry, in either order:

```csharp
builder.Services.AddInventory().WithSerilog().WithOpenTelemetry();
```

`WithSerilog()` hands every event to the module's other logging providers, which includes the OpenTelemetry one. Each entry is exported once.

### Collect more

```csharp
telemetry.AdditionalActivitySources.Add("MyCompany.*");
telemetry.AdditionalMeters.Add("MyCompany.Billing");

// Anything else the OpenTelemetry SDK offers, e.g. an instrumentation package for database spans:
telemetry.ConfigureTracing = tracing => tracing.AddSqlClientInstrumentation();
```

By default, besides every module, it traces `Microsoft.AspNetCore` (incoming requests, usually the root of a trace), `Microsoft.EntityFrameworkCore`, `System.Net.Http` and `Azure.*`. Its default meters are ASP.NET Core hosting and Kestrel, `System.Net.Http` and `System.Runtime`. Database calls need an instrumentation package, such as `OpenTelemetry.Instrumentation.SqlClient`, to produce spans.

### Export to Azure Monitor

Add `Polochon.Telemetry.AzureMonitor` and chain it after this package:

```csharp
builder.Services.AddPolochon().WithOpenTelemetry().WithAzureMonitor();
```

## What a module gets

| Signal | Name | Tags |
|---|---|---|
| Trace, one per command and query | the message type, e.g. `CreateItemCommand` | `polochon.module`, `polochon.message.type`, `polochon.message.kind`, `polochon.result_code`, `error.type` |
| Histogram `polochon.message.duration` (s) | | same as the trace |
| Trace per `ExternalResourceProxy` call (`Client`) | `{resource type} {operation}` | `polochon.module`, `polochon.dependency.*`, `error.type` |
| Histogram `polochon.dependency.duration` (s) | | same as the trace |
| Logs (module-level `WithOpenTelemetry()`) | | `polochon.module`, plus the trace and span of the message being handled |

## Options

| Option | Default | Description |
|---|---|---|
| `ServiceName` | `OTEL_SERVICE_NAME`, else the process | Service name exported with every signal. |
| `AdditionalActivitySources` | ASP.NET Core, EF Core, HttpClient, `Azure.*` | Sources traced besides `Polochon.Modules.*`. Wildcards allowed. |
| `AdditionalMeters` | ASP.NET Core hosting and Kestrel, HttpClient, runtime | Meters collected besides `Polochon.Modules.*`. |
| `SamplingRate` | `1.0` | Ratio of traces started here that are kept, 0.0 to 1.0. |
| `OtlpEndpoint` | none (uses `OTEL_EXPORTER_OTLP_ENDPOINT` if set) | OTLP collector endpoint for every signal. |
| `OtlpProtocol` | gRPC | Protocol used with `OtlpEndpoint`. |
| `EnableConsoleExporter` | `false` | Also write every signal to the console. |
| `EnableMetrics` | `true` | Collect metrics. |
| `EnableLogging` | `true` | Export logs. The module-level `WithOpenTelemetry()` needs it and fails at startup without it. |
| `ConfigureTracing` / `ConfigureMetrics` / `ConfigureLogging` | none | Extra SDK configuration, applied last. |
