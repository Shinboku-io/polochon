# Polochon.FeatureManagement.Azure

Loads a Polochon host's feature flags from [Azure App Configuration](https://learn.microsoft.com/azure/azure-app-configuration/overview) and keeps them refreshed while the host runs.

Only the host is configured. The store endpoint, identity, labels and Key Vault access all stay in host startup code, and modules keep evaluating flags through `WithFeatureManagement()` exactly as they do with appsettings-based flags. Switching a host between appsettings and Azure App Configuration changes no module.

```text
Azure App Configuration --(endpoint, identity, label, refresh)--> host IConfiguration
                                                                        |
                                                  host IFeatureDefinitionProvider
                                                                        |
                                      forwarded by WithFeatureManagement() into each module
                                                                        |
                                    module IFeatureManager / IVariantFeatureManager (+ module filters)
```

## Install

```sh
dotnet add package Polochon.FeatureManagement.Azure
```

## Quick start

Host (`Program.cs`):

```csharp
using Polochon;
using Polochon.FeatureManagement;
using Polochon.FeatureManagement.Azure;

var builder = WebApplication.CreateBuilder(args); // or Host.CreateApplicationBuilder(args)

builder.Services.AddPolochon();

builder.AddAzureAppConfigurationFeatureFlags(options =>
{
    options.Endpoint = new Uri(builder.Configuration["AppConfiguration:Endpoint"]!);
    options.Label = builder.Environment.EnvironmentName;
});

builder.Services.AddInventory().WithFeatureManagement();
```

`AddAzureAppConfigurationFeatureFlags()` adds the store as a configuration source (feature flags only), registers feature management on the host, and starts a background service that refreshes the flags. You don't need `AddPolochon().WithFeatureManagement()` on top. Calling it anyway, before or after, to add host-level filters is safe: it reuses this registration.

A module handler just injects `IFeatureManager`:

```csharp
internal sealed class ListItemQueryHandler : IQueryHandler<ListItemQuery, IReadOnlyList<Item>>
{
    private readonly IReadInventoryItemRepository repository;
    private readonly IFeatureManager featureManager;

    public ListItemQueryHandler(IReadInventoryItemRepository repository, IFeatureManager featureManager)
    {
        this.repository = repository;
        this.featureManager = featureManager;
    }

    public async ValueTask<IReadOnlyList<Item>> HandleAsync(ListItemQuery request, CancellationToken cancellationToken)
    {
        var ascending = await featureManager.IsEnabledAsync("DefaultSortDescending")
            ? false
            : request.Ascending;
        var page = await repository.ListAsync(new ListItemsSpecification(request.SortBy, ascending), cancellationToken);
        return page.CurrentPage;
    }
}
```

The module reads `DefaultSortDescending`, which the store holds as `inventory.DefaultSortDescending`. Each module only sees the flags named after it (`{module name}.{flag}`) and reads them by their short name, so name flags in the store with the module's prefix.

## Samples

### Authenticate with Managed Identity in Azure and your own identity locally

With `Endpoint` set and no `Credential`, the package uses a `DefaultAzureCredential`. It follows `AZURE_TOKEN_CREDENTIALS` when that variable is set, like `Polochon.Messaging.AzureQueue` does:

| Where | `AZURE_TOKEN_CREDENTIALS` | Identity used |
|---|---|---|
| Azure (App Service, Container Apps, AKS...) | `prod` | The resource's Managed Identity. Set `AZURE_CLIENT_ID` too for a user-assigned one. |
| Developer machine | `dev` | Whoever is signed in to Visual Studio or the Azure CLI (`az login`). |
| Not set | - | The full default chain. |

Grant that identity the **App Configuration Data Reader** role on the store.

To use a specific credential instead:

```csharp
builder.AddAzureAppConfigurationFeatureFlags(options =>
{
    options.Endpoint = new Uri("https://my-store.azconfig.io");
    options.Credential = new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId("<client-id>"));
});
```

### Local development with a connection string

A connection string embeds an access key, so keep it in [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), never in `appsettings.json`:

```sh
dotnet user-secrets set "ConnectionStrings:AppConfiguration" "Endpoint=https://my-store.azconfig.io;Id=...;Secret=..."
```

```csharp
builder.AddAzureAppConfigurationFeatureFlags(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("AppConfiguration");
    }
    else
    {
        options.Endpoint = new Uri(builder.Configuration["AppConfiguration:Endpoint"]!);
    }
});
```

Set exactly one of `Endpoint` or `ConnectionString`. Setting neither or both throws at startup.

### Per-environment values with labels

```csharp
options.Label = builder.Environment.EnvironmentName; // "Development", "Staging", "Production"...
```

Flags with no label are loaded first, then flags carrying the label override them. A flag therefore only needs a labelled copy in the environments where it differs:

```sh
# On everywhere by default...
az appconfig feature set   --name my-store --feature inventory.BulkImport --yes
az appconfig feature enable --name my-store --feature inventory.BulkImport --yes

# ...except in Production.
az appconfig feature set    --name my-store --feature inventory.BulkImport --label Production --yes
az appconfig feature disable --name my-store --feature inventory.BulkImport --label Production --yes
```

### Keep appsettings as a fallback, or start without the store

Flags from the store land in the host's configuration in Microsoft's `feature_management` schema, next to flags from other configuration sources. Keep local flags in the .NET `FeatureManagement` schema:

```json
{
  "FeatureManagement": {
    "inventory.BulkImport": false,
    "inventory.LocalOnlyExperiment": true
  }
}
```

- A flag defined only in appsettings keeps working.
- A flag defined in both places takes the store's value, **whatever order the sources were added in**. Microsoft.FeatureManagement reads the `feature_management` schema first.

By default the host fails to start if the store can't be reached. To start anyway, falling back to the other sources until a refresh succeeds:

```csharp
options.Optional = true;
```

### Variants

Variants are defined in the store and evaluated in the module through `IVariantFeatureManager`:

```csharp
var variant = await variantFeatureManager.GetVariantAsync("ListPageSize", cancellationToken);
var pageSize = variant?.Configuration?.Get<int>() ?? 20;
```

### A feature filter only one module uses

Filters are evaluated inside the module, so each module registers its own. A module can't use a filter registered on the host or in another module.

```csharp
[FilterAlias("Schools")]
internal sealed class SchoolsFilter : IContextualFeatureFilter<SchoolContext>
{
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext featureContext, SchoolContext school)
    {
        var schools = featureContext.Parameters.GetSection("Schools").Get<string[]>() ?? [];
        return Task.FromResult(schools.Contains(school.Code, StringComparer.OrdinalIgnoreCase));
    }
}
```

```csharp
// Host
builder.Services.AddInventory()
    .WithFeatureManagement(featureManagement => featureManagement.AddFeatureFilter<SchoolsFilter>());

// Module
var enabled = await featureManager.IsEnabledAsync("BulkImport", new SchoolContext { Code = "LYC-042" });
```

The built-in filters (`Microsoft.Percentage`, `Microsoft.TimeWindow`) are available in every module without registration.

### Also load settings and Key Vault references

By default only feature flags are loaded, so the store's other key-values aren't imported into the host's configuration. To load them too:

```csharp
builder.AddAzureAppConfigurationFeatureFlags(options =>
{
    options.Endpoint = new Uri("https://my-store.azconfig.io");
    options.IncludeKeyValues = true;
    options.ConfigureProvider = provider => provider
        .Select("Inventory:*")                                   // which settings (default: every key with no label)
        .Select("Inventory:*", builder.Environment.EnvironmentName)
        .ConfigureRefresh(refresh => refresh.RegisterAll());      // refresh them along with the flags
});
```

Key Vault references among those settings are resolved with the same `Credential` (or the default one). Grant that identity the **Key Vault Secrets User** role on the vault. As with flags, these settings reach the host's `IConfiguration` only, never a module's.

### Anything else the provider supports

`ConfigureProvider` runs after Polochon's own settings, with full access to `AzureAppConfigurationOptions`:

```csharp
options.ConfigureProvider = provider => provider
    .ConfigureStartupOptions(startup => startup.Timeout = TimeSpan.FromSeconds(15))
    .ConfigureClientOptions(client => client.Retry.MaxRetries = 3);
```

## Refresh

A background service asks the store for changes every `RefreshInterval` (30 seconds by default). A refresh that fails (store unreachable, throttled...) keeps the last known flags and is retried on the next tick. Modules read definitions through the host on each evaluation, so they pick up a change as soon as the host has it, with no restart.

A background service is used instead of the provider's ASP.NET Core middleware because it also works in hosts with no HTTP requests (worker services), and a Blazor Server circuit barely goes through the request pipeline.

## Options

| Option | Default | Description |
|---|---|---|
| `Endpoint` | - | Store endpoint, authenticated with `Credential`. Set this or `ConnectionString`. |
| `ConnectionString` | - | Store connection string (local development). Set this or `Endpoint`. |
| `Credential` | `DefaultAzureCredential` | Identity for the store (with `Endpoint`) and for Key Vault references. |
| `Label` | none | Label whose flags override the unlabelled ones, typically the environment name. |
| `RefreshInterval` | 30 s | How often flags are refreshed. At least 1 second. |
| `Optional` | `false` | Start even if the store is unreachable at startup. |
| `IncludeKeyValues` | `false` | Load plain key-values too, not only feature flags. |
| `ConfigureProvider` | none | Extra `AzureAppConfigurationOptions` configuration, applied last. |
