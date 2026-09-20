# Shinboku Polochon

Open source .NET kernel for building modular monoliths: a CQRS mediator, isolated per-module DI containers, and pluggable cross-cutting concerns (logging today, more to come).

- Hosted publicly on GitHub at `https://github.com/Shinboku-io/polochon.git`.
- Published as `Polochon.*` NuGet packages.
- Consumable by applications through local source references during development and package references for released versions.

**Status:** pre-1.0 / alpha. Public APIs (including registration extension methods) may still change between releases.

## Packages

| Package | What it's for |
|---|---|
| `Polochon` | The core kernel: the `IPolochonDispatcher` mediator, `ModuleBase` (isolated per-module DI container), module registration (`AddModule<TModule>()`), base logging, and the EF Core `UnitOfWork<TContext>`/`GenericRepository<T, TIdentifier>` persistence layer. |
| `Polochon.Abstractions` | Shared contracts with no implementation: the CQRS interfaces (`IQuery`, `ICommand`, handlers, ...), the module interfaces (`IModularModule`, `IModularModuleBuilder<TModule>`), and the domain-modeling base types (`Entity<TIdentifier>`, `ValueObject`, `IDomainEvent`/`IIntegrationEvent`). Reference this from a module's application/domain layer if it shouldn't depend on the kernel implementation. |
| `Polochon.Serilog` | Swaps Polochon's default console logging for Serilog - at the host level, or independently per module. |
| `Polochon.Persistence.SqlServer` | Swaps a module's default EF Core provider for SQL Server - module-level only, since (unlike logging) there's no host-level persistence default to swap. |

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
public sealed class GreetingEndpoint(IPolochonDispatcher dispatcher)
{
    public async Task<EchoResult> Get(string message, CancellationToken cancellationToken)
        => await dispatcher.SendQueryAsync(new EchoQuery { Message = message }, cancellationToken);
}
```

`IPolochonDispatcher` (registered by `AddPolochon()`) finds the module that can handle the query and routes to it - callers never need to know, or reference, which module owns a given query or command.

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
    .ConfigureModule((moduleServices, module) => moduleServices.AddSingleton<ISomething, Something>());
```

`module` here is typed as whatever `IModularModuleBuilder<TModule>` was created for - `IModularModule` if you went through the widened `AddGreeting()` wrapper above, or the concrete module type if you call `services.AddModule<GreetingModule>()` directly (or your wrapper returns `IModularModuleBuilder<GreetingModule>` because `GreetingModule` is `public`).

Queued callbacks run after the module's own `ConfigureAdditionalServices` override and before the module's `IServiceProvider` is built, so a caller's configuration always has the final say over the module's own defaults.

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

`UnitOfWork<TContext>` is generic over your `DbContext` and owns one instance of it for its entire lifetime, created via `IDbContextFactory<TContext>` rather than resolved from the ambient DI scope - so it stays short-lived even inside a DI scope that might outlive it (a Blazor Server circuit, for example). Repositories in the same unit of work must resolve the *same* context instance, via `UnitOfWork<TContext>.Context`:

```csharp
services.AddDbContextFactory<OrdersDbContext>(options => options.UseSqlServer(connectionString), ServiceLifetime.Scoped);

services.AddScoped<UnitOfWork<OrdersDbContext>>();
services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork<OrdersDbContext>>());

// Repositories (and anything else) that need the context resolve this - the same instance
// the unit of work above owns and commits - instead of each creating their own.
services.AddScoped(sp => sp.GetRequiredService<UnitOfWork<OrdersDbContext>>().Context);
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

Then, at the end of a use case:

```csharp
await orderRepository.AddAsync(order, cancellationToken);
await unitOfWork.CommitAsync(cancellationToken); // dispatches domain events, saves, publishes integration events
```

`CommitAsync` collects and dispatches domain events in a loop - if a handler reacting to one event raises another (directly, or by mutating a second tracked aggregate), that event is collected and dispatched too, instead of being silently dropped.

`RollbackAsync` discards every tracked change without ever calling `SaveChanges` - by disposing the current `Context` outright and replacing it with a freshly created one from the same `IDbContextFactory<TContext>`. Clearing the change tracker instead would stop tracking modified entities without undoing the in-memory property changes application code already made to them, and EF Core has no general, reliable way to revert an arbitrary tracked graph (relationship changes especially) one entity at a time - discarding the context is the only way to guarantee a true reset. The consequence: anything obtained through the old context, including entities a repository returned earlier in the same unit of work, is no longer valid - re-resolve `Context` (and any repository built on it) after rolling back, don't keep using what you had before the call.

An aggregate that needs to react to its own deletion implements `IRaiseEventOnDelete`; `CommitAsync` calls `OnDelete()` on every entity tracked as `Deleted` before collecting events, so the event it raises there is dispatched normally.

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

- `src/`: production projects (`Polochon`, `Polochon.Abstractions`, `Polochon.Serilog`, `Polochon.Persistence.SqlServer`).
- `tests/`: test projects.
- `samples/`: package usage samples (placeholder for now).
- `docs/`: public technical documentation (placeholder for now).

## License

MIT - see [LICENSE](LICENSE) for details.
