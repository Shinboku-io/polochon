# Shinboku Polochon

Open source .NET kernel for building modular monoliths: a CQRS mediator, isolated per-module DI containers, and pluggable cross-cutting concerns (logging today, more to come).

- Hosted publicly on GitHub at `https://github.com/Shinboku-io/polochon.git`.
- Published as `Polochon.*` NuGet packages.
- Consumable by applications through local source references during development and package references for released versions.

**Status:** pre-1.0 / alpha. Public APIs (including registration extension methods) may still change between releases.

## Packages

| Package | What it's for |
|---|---|
| `Polochon` | The core kernel: the `IPolochonDispatcher` mediator, `ModuleBase` (isolated per-module DI container), module registration (`AddModule<TModule>()`), base logging, per-module telemetry (traces and metrics of every command and query), module feature flags (`WithFeatureManagement()`), and the EF Core `UnitOfWork<TContext>`/`GenericRepository<T, TIdentifier>` persistence layer. |
| `Polochon.Abstractions` | Shared contracts with no implementation: the CQRS interfaces (`IQuery`, `ICommand`, handlers, ...), the module interfaces (`IModularModule`, `IModularModuleBuilder<TModule>`), and the domain-modeling base types (`Entity<TIdentifier>`, `ValueObject`, `IDomainEvent`/`IIntegrationEvent`). Reference this from a module's application/domain layer if it shouldn't depend on the kernel implementation. |
| `Polochon.FeatureManagement.AzureAppConfiguration` | Makes an Azure App Configuration store the source of the host's feature flags (endpoint, identity, labels, refresh, Key Vault) - host-level only, so none of that plumbing reaches modules. |
| `Polochon.Serilog` | Swaps Polochon's default console logging for Serilog - at the host level, or independently per module. |
| `Polochon.Persistence.SqlServer` | Swaps a module's default EF Core provider for SQL Server - module-level only, since (unlike logging) there's no host-level persistence default to swap. |
| `Polochon.Validation.FluentValidation` | Runs a module's FluentValidation validators as Polochon message validators, reporting the first failure as a `ResultCode`. |

## Quickstart

### 1. Install

```sh
dotnet add package Polochon
```

### 2. Wire up the host

```csharp
var builder = WebApplication.CreateBuilder(args); // or Host.CreateApplicationBuilder(args)

builder.Services.AddPolochon();
```

`AddPolochon()` registers the host-level `IPolochonDispatcher` (it routes every query/command to whichever registered module can handle it) and Polochon's base logging pipeline (a console provider via `Microsoft.Extensions.Logging`), so logging works out of the box with no further setup.

### 3. Define a query (or command) and its handler

```csharp
using Polochon.Abstractions.CQRS;

public record EchoQuery : IQuery<EchoResult>
{
    public string Message { get; init; } = string.Empty;
}

public record EchoResult
{
    public string Message { get; init; } = string.Empty;
}

public sealed class EchoQueryHandler : IQueryHandler<EchoQuery, EchoResult>
{
    public async ValueTask<EchoResult> HandleAsync(EchoQuery query, CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken); // do the actual work here
        return new EchoResult { Message = query.Message.ToUpperInvariant() };
    }
}
```

Commands follow the same shape with `ICommand`/`ICommand<TResponse>` and `ICommandHandler<>`/`ICommandHandler<,>`. Fire-and-forget events use `INotification`/`INotificationHandler<>` and are published through `INotificationPublisher` - unlike a query or command, a notification may have zero, one, or many handlers, and publishing to zero is a normal no-op, not a failure.

### 4. Create a module

Each module owns a fully isolated `IServiceCollection`/`IServiceProvider` - nothing outside the module (not even the host) can see or resolve its internal services directly.

```csharp
using Polochon.Modules;

internal sealed class GreetingModule : ModuleBase
{
    public GreetingModule()
        : base("greeting", [typeof(EchoQuery).Assembly])
    {
    }
}
```

The assembly passed to the base constructor is scanned (by reflection, once, at startup) for `IQueryHandler<,>`, `ICommandHandler<>`/`ICommandHandler<,>`, `INotificationHandler<>` and `IMessageValidator<,>` implementations, which are wired up automatically - no manual registration needed for handlers.

### 5. Register the module

```csharp
using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.Modules;
using Polochon.Modules;

public static class ServiceRegister
{
    public static IModularModuleBuilder<IModularModule> AddGreeting(this IServiceCollection services)
        => services.AddModule<GreetingModule>();
}
```

```csharp
// Program.cs
builder.Services.AddGreeting();
```

A domain-specific wrapper like `AddGreeting()` above is optional but conventional - it's what shows up in `Program.cs`, and it keeps `AddModule<GreetingModule>()` (and the concrete `GreetingModule` type, which can stay `internal`) out of the host's direct view.

> `services.AddModule<GreetingModule>()` itself returns the strongly-typed `IModularModuleBuilder<GreetingModule>`. `AddGreeting()` above widens that to `IModularModuleBuilder<IModularModule>` (the covariant conversion is implicit, no cast needed) because `GreetingModule` is `internal` - a `public` method can't return a type parameterized by an internal type. If your module type is `public`, your wrapper can return `IModularModuleBuilder<TModule>` directly instead, and callers get the concrete module type back from `ConfigureModule` (see below).

### 6. Send a query from anywhere in the host

```csharp
public sealed class GreetingEndpoint
{
    private readonly IPolochonDispatcher dispatcher;

    public GreetingEndpoint(IPolochonDispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
    }

    public async Task<EchoResult> Get(string message, CancellationToken cancellationToken)
        => await dispatcher.SendQueryAsync(new EchoQuery { Message = message }, cancellationToken);
}
```

`IPolochonDispatcher` (registered by `AddPolochon()`) finds the module that can handle the query and routes to it - callers never need to know, or reference, which module owns a given query or command.

## Message scope: return fully resolved objects

Every query or command sent to a module is handled in a DI scope of its own, created for that message and disposed as soon as it has been handled: its handler gets fresh scoped services (unit of work, `DbContext`, repositories...) instead of sharing them with any other message. A message a handler sends through its own injected `IPolochonDispatcher` stays in that same scope.

The consequence, implicit with aggregates but easy to miss: **whatever a module returns must be fully resolved before it leaves the handler**, because the context that produced it is gone by the time the caller reads it.

- **Materialize collections** - return an `IReadOnlyList<T>` built with `ToListAsync`, never an `IQueryable<T>`, a lazily evaluated `IEnumerable<T>` or an `IAsyncEnumerable<T>` still bound to the context.
- **Load everything the caller will read** - include the related data in the query (a specification, or `GenericRepository`'s `ApplyIncludes`). A navigation property left unloaded stays empty, and a lazy-loading proxy would throw `ObjectDisposedException`.
- **Never hand out scoped services** - no `DbContext`, unit of work or repository, not even wrapped in another object.
- **Treat returned aggregates as read-only snapshots** - they are detached from any unit of work, so changing one persists nothing. Changes go through a command. When the caller only displays data, prefer returning a dedicated read model or DTO over the aggregate itself.

## Logging

Every module gets Polochon's base logging (a console provider) automatically, with no setup required.

### Swap the whole app to Serilog

```sh
dotnet add package Polochon.Serilog
```

```csharp
using Polochon.Serilog;

builder.Services.AddPolochon().WithSerilog();

// or customize/extend Polochon's base Serilog configuration:
builder.Services.AddPolochon().WithSerilog(config => config
    .MinimumLevel.Warning());
```

(Add another sink, e.g. `dotnet add package Serilog.Sinks.Seq`, to call `.WriteTo.Seq(...)` here too - `Polochon.Serilog` only brings in the console sink by default.)

This also becomes the process-wide `Serilog.Log.Logger`.

### Swap just one module to Serilog

```csharp
builder.Services.AddGreeting().WithSerilog();
```

Each module's Serilog logger is fully independent from the host's and from every other module's - configuring one module's logging never affects another module or the host, and it never touches the global `Serilog.Log.Logger`.

## Configuring a module directly

`AddModule<TModule>()` (and any domain-specific wrapper built on it, like `AddGreeting()` above) returns an `IModularModuleBuilder<TModule>`. Beyond what `Polochon.Serilog` uses it for, any caller can queue their own configuration against the module's isolated container, with the actual module instance handed back:

```csharp
services.AddGreeting()
    .ConfigureModule((moduleServices, module, _) => moduleServices.AddSingleton<ISomething, Something>());
```

The third argument is the host's root `IServiceProvider`, already built when the callback runs. Most callbacks ignore it (`_`); it exists for extensions that bridge one host-owned service into the module's isolated container - as `WithFeatureManagement()` does with the host's feature definitions. Take as little from the host as possible: the module container is isolated on purpose.

`module` here is typed as whatever `IModularModuleBuilder<TModule>` was created for - `IModularModule` if you went through the widened `AddGreeting()` wrapper above, or the concrete module type if you call `services.AddModule<GreetingModule>()` directly (or your wrapper returns `IModularModuleBuilder<GreetingModule>` because `GreetingModule` is `public`).

Queued callbacks run after the module's own `ConfigureAdditionalServices` override and before the module's `IServiceProvider` is built, so a caller's configuration always has the final say over the module's own defaults.

## Feature flags

Feature flags use [Microsoft.FeatureManagement](https://learn.microsoft.com/azure/azure-app-configuration/feature-management-dotnet-reference). The host owns where flag definitions come from; a module only evaluates them:

```csharp
// Host: definitions from the "FeatureManagement" configuration section (appsettings, env vars...).
builder.Services.AddPolochon().WithFeatureManagement();

// Module: IFeatureManager / IVariantFeatureManager in the module's own container.
builder.Services.AddGreeting().WithFeatureManagement();
```

```csharp
internal sealed class GreetQueryHandler : IQueryHandler<GreetQuery, string>
{
    private readonly IFeatureManager featureManager;

    public GreetQueryHandler(IFeatureManager featureManager)
    {
        this.featureManager = featureManager;
    }

    public async ValueTask<string> HandleAsync(GreetQuery request, CancellationToken cancellationToken)
        => await featureManager.IsEnabledAsync("FriendlyGreeting")
            ? $"Hey {request.Name}!"
            : $"Hello, {request.Name}.";
}
```

`WithFeatureManagement()` gives the module an `IFeatureDefinitionProvider` that forwards to the host's, so the module never sees the host's `IConfiguration`, endpoints or credentials. Definitions are read through on each evaluation, so a configuration reload on the host reaches every module. Feature filters, targeting and variants are still evaluated inside the module: `WithFeatureManagement(fm => fm.AddFeatureFilter<MyFilter>())` adds a filter to that module only. If the host has not registered feature management, module initialization fails at startup, naming the module.

To load the host's flags from Azure App Configuration instead, see [`Polochon.FeatureManagement.AzureAppConfiguration`](src/Polochon.FeatureManagement.AzureAppConfiguration/README.md) - modules do not change.

## Telemetry

Polochon emits telemetry through the standard .NET APIs - `ActivitySource` for traces, `Meter` for metrics, `ILogger` for logs - so any exporter collects it without an adapter. Nothing is exported until the host configures an exporter; when nothing listens, the instrumentation costs next to nothing.

Every module gets, with no registration:

- **`IModuleTelemetry`** in its container: an `ActivitySource` and a `Meter`, both named `Polochon.Modules.{module name}`.
- **A trace of every command and query it handles**, one activity per message named after the message type, tagged `polochon.module`, `polochon.message.type`, `polochon.message.kind` (`command`/`query`) and, for a `CommandResult`, `polochon.result_code`. A thrown exception or a failed `ResultCode` sets the activity's status to error and `error.type`. A message a handler sends through its injected dispatcher is traced as a child.
- **The `polochon.message.duration` histogram** (seconds, same tags): its count is the number of messages handled.

The tracing behavior runs outermost in the pipeline, so it covers validators and the handler, and a command failure that `CommandResultBehavior` turns into a `ResultCode` is still traced as an error.

A module adds its own telemetry through `IModuleTelemetry`:

```csharp
internal sealed class ImportItemsCommandHandler : ICommandHandler<ImportItemsCommand, CommandResult>
{
    private readonly ActivitySource activitySource;
    private readonly Counter<int> importedItems;

    public ImportItemsCommandHandler(IModuleTelemetry telemetry)
    {
        activitySource = telemetry.ActivitySource;
        importedItems = telemetry.Meter.CreateCounter<int>("inventory.items.imported");
    }

    public async ValueTask<CommandResult> HandleAsync(ImportItemsCommand command, CancellationToken cancellationToken)
    {
        using var activity = activitySource.StartActivity("ParseFile"); // null when nothing listens
        // ...
        importedItems.Add(command.Items.Count);
        return CommandResult.Success();
    }
}
```

To trace calls to an external resource that has no instrumentation of its own, derive from `ExternalResourceProxy<TResource>`. Each call becomes a `Client` activity of the module, timed in the `polochon.dependency.duration` histogram, with failures recorded on the activity and rethrown as is. HttpClient, EF Core, SqlClient and most Azure SDK clients are already instrumented, so don't wrap those.

```csharp
internal sealed class SmtpProxy : ExternalResourceProxy<SmtpClient>
{
    public SmtpProxy(SmtpClient client, IModuleTelemetry telemetry)
        : base(client, telemetry, "smtp", "outbound-mail")
    {
    }

    public Task SendAsync(MailMessage message) => TelemetryCallAsync("Send", client => client.SendMailAsync(message));
}
```

Exporters are configured once, on the host, and collect every module by name (`PolochonTelemetry.AllModulesSourceName`, i.e. `Polochon.Modules.*`). Tag and metric names are constants on `PolochonTelemetry`.

## Persistence: unit of work, domain and integration events

An aggregate root derives from `Entity<TIdentifier>` to get identity plus the ability to raise events:

```csharp
using Polochon.Abstractions.Domain;

public sealed class Order : Entity<OrderId>
{
    private Order(OrderId id) : base(id) { }

    public static Order Place(OrderId id, decimal total)
    {
        var order = new Order(id);
        order.AddEvent(new OrderPlacedDomainEvent(id, total));
        return order;
    }
}
```

`AddEvent` (protected - call it from within the aggregate) accepts either kind of event:

- **`IDomainEvent`** models an internal invariant or reaction. It's dispatched *in-process, synchronously, before the transaction commits* - a handler can still influence what gets persisted, and a handler that throws aborts the commit.
- **`IIntegrationEvent`** is a versioned fact for *other modules*. It's only published *after* the commit succeeds, so a rolled-back transaction never leaks an event.

A single event type must not implement both interfaces - `AddEvent` throws if it does. The two model different concerns on purpose: a domain event's shape is free to change with the aggregate's internals, while an integration event is a contract other modules take a dependency on. If a domain event needs a cross-module consequence, raise a second, distinct integration event instead.

### Wiring up the unit of work

`UnitOfWork<TContext>` is generic over your `DbContext` and owns one instance of it for its entire lifetime, created via `IDbContextFactory<TContext>` rather than resolved from the ambient DI scope - so it stays short-lived even inside a DI scope that might outlive it (a Blazor Server circuit, for example). Repositories in the same unit of work must use the *same* context instance, `UnitOfWork<TContext>.Context`.

Writes go through the unit of work: derive a module-specific one that exposes the module's mutable repositories, and have command handlers depend on its interface - never on a mutable repository directly. Reads go through read-only repositories (see [Reading through a repository](#reading-through-a-repository)).

```csharp
// Application layer: the write port command handlers depend on.
public interface IOrdersUnitOfWork : IUnitOfWork
{
    IOrderRepository Orders { get; }
}
```

```csharp
// Infrastructure layer.
internal sealed class OrdersUnitOfWork : UnitOfWork<OrdersDbContext>, IOrdersUnitOfWork
{
    public OrdersUnitOfWork(
        IDbContextFactory<OrdersDbContext> contextFactory,
        IOutboxWriter outbox,
        INotificationPublisher notificationPublisher)
        : base(contextFactory, outbox, notificationPublisher)
    {
    }

    // Built over the current Context on each access (a repository is cheap to create), so it
    // follows RollbackAsync replacing the context instead of holding on to the disposed one.
    public IOrderRepository Orders => new OrderRepository(Context);
}
```

```csharp
services.AddDbContextFactory<OrdersDbContext>(options => options.UseSqlServer(connectionString), ServiceLifetime.Scoped);

services.AddScoped<OrdersUnitOfWork>();
services.AddScoped<IOrdersUnitOfWork>(sp => sp.GetRequiredService<OrdersUnitOfWork>());

// Anything else that needs the context resolves this - the same instance the unit of work
// above owns and commits - instead of creating its own.
services.AddScoped(sp => sp.GetRequiredService<OrdersUnitOfWork>().Context);

// Read side: only the read-only interface is registered. The mutable one is reachable through
// IOrdersUnitOfWork alone, so nothing can write outside a unit of work.
services.AddScoped<IReadOnlyRepository<Order, OrderId>, OrderRepository>();
```

The lifetime matters: EF's own default is `Singleton`, which means an options-configuration callback (the `Action<IServiceProvider, DbContextOptionsBuilder>` overload) would only ever see the *root* provider - it could never safely resolve a scoped service (a per-request/per-tenant connection-string resolver, for example). `ServiceLifetime.Scoped` matches every other registration in this graph above.

In your `DbContext`'s `OnModelCreating`, ignore the two event collections on every `Entity<TIdentifier>`-derived aggregate - otherwise EF Core's model builder tries (and fails) to map them as navigation properties:

```csharp
using Polochon.Persistence; // IgnoreRaisedEvents

modelBuilder.Entity<Order>(builder =>
{
    builder.HasKey(o => o.Identifier);
    builder.IgnoreRaisedEvents();
});
```

Then, in a command handler:

```csharp
public sealed class PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand, CommandResult>
{
    private readonly IOrdersUnitOfWork unitOfWork;

    public PlaceOrderCommandHandler(IOrdersUnitOfWork unitOfWork)
    {
        this.unitOfWork = unitOfWork;
    }

    public async ValueTask<CommandResult> HandleAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        var order = Order.Place(command.OrderId, command.Total);

        await unitOfWork.Orders.AddAsync(order, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken); // dispatches domain events, saves, publishes integration events

        return CommandResult.Success();
    }
}
```

With the command itself a record, in its own file (as is the handler):

```csharp
public sealed record PlaceOrderCommand : ICommand<CommandResult>
{
    public required OrderId OrderId { get; init; }

    public required decimal Total { get; init; }
}
```

`CommitAsync` collects and dispatches domain events in a loop - if a handler reacting to one event raises another (directly, or by mutating a second tracked aggregate), that event is collected and dispatched too, instead of being silently dropped.

`RollbackAsync` discards every tracked change without ever calling `SaveChanges` - by disposing the current `Context` outright and replacing it with a freshly created one from the same `IDbContextFactory<TContext>`. Clearing the change tracker instead would stop tracking modified entities without undoing the in-memory property changes application code already made to them, and EF Core has no general, reliable way to revert an arbitrary tracked graph (relationship changes especially) one entity at a time - discarding the context is the only way to guarantee a true reset. The consequence: anything obtained through the old context, including entities a repository returned earlier in the same unit of work, is no longer valid - re-resolve `Context` (and any repository built on it) after rolling back, don't keep using what you had before the call. A repository exposed by the unit of work as above, built over `Context` on each access, follows automatically.

An aggregate that needs to react to its own deletion implements `IRaiseEventOnDelete`; `CommitAsync` calls `OnDelete()` on every entity tracked as `Deleted` before collecting events, so the event it raises there is dispatched normally.

### Reading through a repository

`GenericRepository<T, TIdentifier>` implements `IReadOnlyRepository<,>` and `IMutableRepository<,>` over the unit of work's context. Derive from it once per aggregate root, passing a selector for the mapped identifier property:

```csharp
// Application layer: the module's mutable repository port, exposed by IOrdersUnitOfWork.
public interface IOrderRepository : IMutableRepository<Order, OrderId>
{
}
```

```csharp
// Infrastructure layer.
internal sealed class OrderRepository : GenericRepository<Order, OrderId>, IOrderRepository
{
    public OrderRepository(OrdersDbContext context)
        : base(context, order => order.Identifier)
    {
    }
}
```

Query handlers depend on the read-only interface only - never on the unit of work - and return fully resolved objects (see [Message scope](#message-scope-return-fully-resolved-objects)). Every repository method takes a `CancellationToken`, with no default value - pass the one your handler received, so that a caller giving up (an aborted HTTP request, a closed Blazor circuit, a timeout) also cancels the database round trip instead of letting it run to completion for nobody:

```csharp
public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, Order?>
{
    private readonly IReadOnlyRepository<Order, OrderId> orders;

    public GetOrderQueryHandler(IReadOnlyRepository<Order, OrderId> orders)
    {
        this.orders = orders;
    }

    public async ValueTask<Order?> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken)
        => await orders.GetByIdAsync(query.OrderId, cancellationToken);
}
```

The token reaches every query EF Core sends for `GetByIdAsync`, `GetAsync`, `QueryAsync` and both `ListAsync` overloads (the paged one cancels its count query and its page query alike). `GetAsync` and the non-paged `ListAsync` also look through entities added in the current unit of work but not saved yet; that part runs in memory and isn't cancellable - there's nothing to cancel. A cancelled read throws `OperationCanceledException`, which propagates to the caller: commands returning a `CommandResult` rethrow it too, instead of reporting it as `-1`/`UNEXPECTED_ERROR`.

### Swapping a module's provider for SQL Server

```sh
dotnet add package Polochon.Persistence.SqlServer
```

```csharp
using Polochon.Persistence.SqlServer;

services.AddGreeting().WithSqlServer(connectionString);

// or resolve the connection string from the module's own (scoped) provider - e.g. configuration,
// or a per-tenant resolver:
services.AddGreeting().WithSqlServer(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("Orders")!);
```

Unlike `WithSerilog()`, there's no host-level overload: only modules own `DbContext`s, so `WithSqlServer<TModule, TContext>()` is module-level only, layered on `IModularModuleBuilder<TModule>.ConfigureModule(...)` the same way. It always registers with `ServiceLifetime.Scoped` (not configurable), and it's safe to call even after the module already registered a default provider (e.g. `UseInMemoryDatabase` for local dev) - it explicitly removes that prior registration first, since EF Core's `AddDbContextFactory` registers via `TryAdd` internally and would otherwise silently do nothing on a second call.

If your module's `DbContext` type is `internal` (the recommended default - see `app/AGENTS.md`-style infrastructure encapsulation rules in your own app), the host can never call `WithSqlServer<TModule, TContext>()` directly, since it can't name an internal type as a generic argument. Write your own zero-generic-parameter wrapper inside the module's assembly instead, mirroring `AddGreeting()`:

```csharp
// Inside the module's own assembly, where OrdersDbContext (internal) is nameable:
public static IModularModuleBuilder<IModularModule> WithSqlServer(
    this IModularModuleBuilder<IModularModule> builder,
    string connectionString)
    => builder.WithSqlServer<IModularModule, OrdersDbContext>(connectionString);
```

### The outbox is not a durable outbox (yet)

Integration events are published to `IOutbox`, whose default implementation (`MemoryOutbox`) is **an in-memory relay, not a transactional outbox**: published messages live only in the process's memory and are lost on crash or restart. Real, durable delivery to other modules is planned to live in each *consuming* module's own inbox - a future cross-module bus will drain this relay and hand each message off there. Don't depend on `IOutbox` today where losing a buffered message on restart would be unacceptable.

## Repository layout

- `src/`: production projects (`Polochon`, `Polochon.Abstractions`, `Polochon.Serilog`, `Polochon.Persistence.SqlServer`, `Polochon.Messaging.AzureQueue`, `Polochon.Validation.FluentValidation`).
- `tests/`: test projects.
- `samples/`: package usage samples (placeholder for now).
- `docs/`: public technical documentation (placeholder for now).

## License

MIT - see [LICENSE](LICENSE) for details.
