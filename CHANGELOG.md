# Changelog

All notable changes to the Polochon kernel are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

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

### Changed

- **Breaking:** `IModularModuleBuilder<TModule>.ConfigureModule` now takes an
  `Action<IServiceCollection, TModule, IServiceProvider>`; the third argument is the host's root
  service provider. The `Action<IServiceCollection, TModule>` overload was removed.
  Migration: add a discarded third parameter, e.g. `ConfigureModule((services, module) => ...)`
  becomes `ConfigureModule((services, module, _) => ...)`. Name it instead of using `_` when the
  lambda body itself uses a `_ = ...` discard: a lone `_` lambda parameter is a real parameter.
