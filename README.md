# Shinboku Polochon

Open source .NET kernel for building modular monoliths: a CQRS mediator, isolated per-module DI containers, and pluggable cross-cutting concerns (logging today, more to come).

- Hosted publicly on GitHub at `https://github.com/Shinboku-io/polochon.git`.
- Published as `Polochon.*` NuGet packages.
- Consumable by applications through local source references during development and package references for released versions.

**Status:** pre-1.0 / alpha. Public APIs (including registration extension methods) may still change between releases.

## Packages

| Package | What it's for |
|---|---|
| `Polochon` | The core kernel: the `IPolochonDispatcher` mediator, `ModuleBase` (isolated per-module DI container), module registration (`AddModule<TModule>()`), and base logging. |
| `Polochon.Abstractions` | Shared contracts with no implementation: the CQRS interfaces (`IQuery`, `ICommand`, handlers, ...) and the module interfaces (`IModularModule`, `IModularModuleBuilder<TModule>`). Reference this from a module's application/domain layer if it shouldn't depend on the kernel implementation. |
| `Polochon.Serilog` | Swaps Polochon's default console logging for Serilog - at the host level, or independently per module. |

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

Commands follow the same shape with `ICommand`/`ICommand<TResponse>` and `ICommandHandler<>`/`ICommandHandler<,>`. Fire-and-forget events use `INotification`/`INotificationHandler<>` (multiple handlers per notification are allowed).

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

## Repository layout

- `src/`: production projects (`Polochon`, `Polochon.Abstractions`, `Polochon.Serilog`).
- `tests/`: test projects.
- `samples/`: package usage samples (placeholder for now).
- `docs/`: public technical documentation (placeholder for now).

## License

MIT - see [LICENSE](LICENSE) for details.
