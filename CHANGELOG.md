# Changelog

All notable changes to the Polochon kernel are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.6.0] - 2026-10-16

Minor release: new features only, no breaking change.

### Added

- `Polochon.Messaging.AzureQueue`: poison message rejection. `PersistentInboxOptions.MaxDequeueCount`
  (default 5) caps how many times a message can be received from the inbox queue. Past that, the
  message is moved to the error queue unprocessed and deleted, instead of being retried forever.
  This happens when earlier attempts never completed, e.g. a message that crashes the process or
  whose handling outlasts the visibility timeout. A handler that throws still sends the message to
  the error queue on the first attempt, as before.
- `WithInboxProcessing(InboxProcessingOptions)` and `InboxProcessingMode`: an inbox processor can
  now run in `InProcess` mode instead of polling. It reads the module's inbox once at startup, then
  only when this process's `EventBus` delivers a message into it, so a persistent inbox (e.g. Azure
  Storage Queue) costs no reads while the module is idle. Meant for modules hosted in the same
  process as every module publishing to them (a modular monolith); keep `Polling` (the default, and
  the behavior of `WithInboxProcessing(TimeSpan?)`) when other processes deliver into the inbox.
  `InboxProcessingOptions.IdleSweep` optionally reads the inbox after that long without a delivery,
  for messages that become readable again without one (e.g. after an interrupted processing attempt).
  `ModuleBase.Inbox` now raises the module's delivery signal after each write.
- On-demand inbox drain, for a module deployed on its own and started only when its inbox has
  messages (e.g. an Azure Container Apps job scaled by KEDA on the inbox queue) instead of polled by
  `WithInboxProcessing`. `ModuleBase.ProcessInboxAsync()` reads the inbox until it is empty,
  handling each message exactly like `InboxProcessor`, and returns an `InboxDrainResult` (processed
  and failed counts). `EventBus.RelayPendingMessagesAsync()` relays pending outbox messages right
  away instead of on the next bus tick. `IServiceProvider.DrainInboxAsync<TModule>()` chains both, so
  the process can stop without losing the integration events its handlers published.

## [0.5.0] - 2026-10-07

### Added

- `CommandResult.Errors`: every validation failure found when a validator rejected the command,
  filled by `CommandResultBehavior` from `MessageValidationException.Errors`, and
  `CommandResult.Failure(ResultCode, IReadOnlyList<ValidationError>)`.
- `IntegrationEventToCommandHandler.OnCommandHandledAsync` and `IntegrationEventHandlingException`
  (see Changed).
- `Polochon.FeatureManagement`: `WithFeatureManagement()` on a module builder adds
  Microsoft.FeatureManagement (`IFeatureManager`, `IVariantFeatureManager`) to the module's isolated
  container. Feature definitions are forwarded from the host's `IFeatureDefinitionProvider`, so the
  definition source (appsettings, Azure App Configuration, ...) and its plumbing stay host-side.
  Requires the host to register feature management: `services.AddPolochon().WithFeatureManagement()`,
  which reads definitions from configuration and reuses any feature management registered earlier
  (e.g. by the Azure App Configuration package) instead of registering it twice.
- `Polochon` now depends on `Microsoft.FeatureManagement`.
- New package `Polochon.FeatureManagement.Azure`: `AddAzureAppConfigurationFeatureFlags()`
  on the host builder loads feature flags from an Azure App Configuration store (endpoint + Managed
  Identity or connection string, per-environment label, optional Key Vault-backed settings) and
  refreshes them in the background. Host-level only: modules keep using `WithFeatureManagement()`.

- Telemetry, on by default in every module and exporter-agnostic (`System.Diagnostics` APIs):
  `IModuleTelemetry` (an `ActivitySource` and a `Meter` named `Polochon.Modules.{module}`), an
  activity per command and query handled plus the `polochon.message.duration` histogram, and
  `ExternalResourceProxy<TResource>` tracing calls to uninstrumented external resources as client
  activities timed in `polochon.dependency.duration`. Names are constants on `PolochonTelemetry`.
- `Polochon` now depends on `Microsoft.Extensions.Diagnostics` (`IMeterFactory`).
- New package `Polochon.Telemetry.OpenTelemetry`: `services.AddPolochon().WithOpenTelemetry()` sets up
  the host's OpenTelemetry pipeline (every module's traces and metrics, host logs, sampling, OTLP
  when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, console), and the module-level `WithOpenTelemetry()`
  exports a module's logs through it, tagged `polochon.module`.
- New package `Polochon.Telemetry.AzureMonitor`: `.WithAzureMonitor()`, chained after
  `WithOpenTelemetry()`, exports that pipeline to Application Insights, only for the signals the
  host collects and at the host's sampling rate.

### Changed

- **Breaking:** there are no commands without a result any more. `ICommand` is now an alias of
  `ICommand<CommandResult>` (instead of `ICommand<Unit>`), and `ICommandHandler<TCommand>` an alias
  of `ICommandHandler<TCommand, CommandResult>` - so a handler implementing only the one-argument
  interface is now found by the assembly scan (it was silently skipped before). The
  `IPolochonDispatcher.SendCommandAsync(ICommand)` overload returning a bare `ValueTask` was removed:
  `SendCommandAsync` always returns the command's response. Migration: return `CommandResult`
  instead of `Unit` from `ICommand` handlers and validators. Exceptions thrown while handling an
  `ICommand` are now reported as a failed `CommandResult` by `CommandResultBehavior` instead of
  propagating.
- **Breaking:** `ResultCode.IsOk` is now `Code >= 0`: zero or positive codes report a success
  (business success codes such as `ITEM_CREATED`), negative codes a failure. Migration: make every
  module failure code negative. `CommandResult.Success(ResultCode)` reports a business success
  code; `CommandResult.Success(ResultCode)` and `CommandResult.Failure(ResultCode)` throw an
  `ArgumentException` when handed a code of the wrong sign.
- `IntegrationEventToCommandHandler<TEvent, TCommand>` now awaits the mapped command's result and
  hands it to the new `protected virtual OnCommandHandledAsync`. By default, a failed result throws
  the new `IntegrationEventHandlingException`, so the event is still reported as failed (and sent to
  the error queue by the inbox processor) now that command exceptions are reported as results.

- **Breaking:** `IModularModuleBuilder<TModule>.ConfigureModule` now takes an
  `Action<IServiceCollection, TModule, IServiceProvider>`; the third argument is the host's root
  service provider. The `Action<IServiceCollection, TModule>` overload was removed.
  Migration: add a discarded third parameter, e.g. `ConfigureModule((services, module) => ...)`
  becomes `ConfigureModule((services, module, _) => ...)`. Name it instead of using `_` when the
  lambda body itself uses a `_ = ...` discard: a lone `_` lambda parameter is a real parameter.
- **Breaking:** a module only sees its own feature flags: the host's flags named
  `{module name}.{flag}` (prefix matched ignoring case), under their short name. The `inventory`
  module's `IsEnabledAsync("BulkImport")` reads the host's `inventory.BulkImport`; other modules'
  flags and unprefixed flags are invisible to it. Migration: prefix every module flag defined on the
  host (appsettings, Azure App Configuration) with the module name.
- `Polochon.Serilog`: `WithSerilog()`, host and module, now also hands every event to the other
  logging providers of its container (e.g. OpenTelemetry's), and removes Polochon's base console
  provider there, which Serilog's own console sink replaces.
