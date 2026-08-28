# Dispatcher

[![NuGet DependencyInjection](https://img.shields.io/nuget/v/Send0xx.Dispatcher.DependencyInjection.svg?label=DependencyInjection)](https://www.nuget.org/packages/Send0xx.Dispatcher.DependencyInjection/)
[![NuGet SourceGeneration](https://img.shields.io/nuget/v/Send0xx.Dispatcher.SourceGeneration.svg?label=SourceGeneration)](https://www.nuget.org/packages/Send0xx.Dispatcher.SourceGeneration/)
[![CI](https://github.com/send0xx/Dispatcher/actions/workflows/ci.yml/badge.svg)](https://github.com/send0xx/Dispatcher/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/send0xx/Dispatcher/blob/main/LICENSE)

Dispatcher is a small CQRS library for .NET applications that use dependency injection. It provides focused APIs for queries, commands, notifications, handlers, and pipeline behaviors.

## Main features

- **Reflection-free dispatch.** Handler routes are stored in frozen dictionaries, and handlers and pipeline behaviors are resolved from the current dependency-injection scope.
- **Polymorphic routes.** A message uses its exact handler when one exists, and otherwise the most-specific compatible base class or interface handler. Routes are decided during registry creation or at compile time, not per dispatch.
- **Internal handlers and modular composition.** Handlers never have to be public, and an application split across assemblies registers each module's handlers separately, under either registration mode.
- **Trimming and Native AOT support.** The source generator emits registrations and dispatch tables at compile time, and its package does not reference the reflection-based implementation at all.
- **Built-in OpenTelemetry.** Tracing activities and an operation-duration histogram, disabled by default, adding no work to the dispatch path while off.

## Two modes supported

- **Reflection-based registration** for a straightforward application setup.
- **Source-generated registration and dispatch** for trimming and Native AOT.

## Install

For the simplest Microsoft dependency-injection setup, install:

```bash
dotnet add package Send0xx.Dispatcher.DependencyInjection --version 2.0.0
```

For source-generated registration and Native AOT, install instead:

```bash
dotnet add package Send0xx.Dispatcher.SourceGeneration --version 2.0.0
```

Choose one implementation package. Both bring in the abstractions, runtime, and Microsoft DI integration they require.

## Quick start

Define a query and its handler:

```csharp
using Dispatcher;

public sealed record GetGreetingQuery(string Name) : IQuery<string>;

internal sealed class GetGreetingQueryHandler
    : IQueryHandler<GetGreetingQuery, string>
{
    public ValueTask<string> HandleAsync(
        GetGreetingQuery query,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult($"Hello, {query.Name}!");
}
```

Register Dispatcher and scan the application assembly for handlers:

```csharp
using Dispatcher.DependencyInjection;

builder.Services
    .AddDispatcher()
    .AddDispatcherHandlers(typeof(Program).Assembly);
```

`AddDispatcher()` registers infrastructure only and never scans assemblies implicitly.
`AddDispatcherHandlers()` registers internal handler classes. Registering the same assembly more than once is safe.

Inject a focused dispatcher interface and send the query:

```csharp
app.MapGet("/greetings/{name}", async (
    string name,
    IQueryDispatcher queries,
    CancellationToken cancellationToken) =>
{
    var greeting = await queries.QueryAsync(
        new GetGreetingQuery(name),
        cancellationToken);

    return Results.Ok(greeting);
});
```

Four dispatcher interfaces are available. Inject the narrowest one a class needs:

| Interface | Method | Dispatches |
| --- | --- | --- |
| `IQueryDispatcher` | `QueryAsync` | Queries |
| `ICommandDispatcher` | `ExecuteAsync` | Commands, with or without a response |
| `INotificationDispatcher` | `PublishAsync` | Notifications |
| `IDispatcher` | all of the above | Anything, for classes that need more than one kind |

## Registration and lifetimes

Dispatcher and handlers are scoped by default. Resolve them inside a DI scope, as ASP.NET Core does for each request.

A single handler can be registered explicitly instead of scanning for it:

```csharp
using Dispatcher;

builder.Services.AddQueryHandler<GetGreetingQuery, string, GetGreetingQueryHandler>();
```

There is one method per handler kind: `AddQueryHandler`, `AddCommandHandler`, and `AddNotificationHandler`. Each takes an optional delegate that sets the handler lifetime, and assembly scanning takes the same delegate:

```csharp
builder.Services.AddQueryHandler<GetGreetingQuery, string, GetGreetingQueryHandler>(options =>
    options.ServiceLifetime = ServiceLifetime.Singleton);

builder.Services.AddDispatcherHandlers(
    typeof(Program).Assembly,
    options => options.ServiceLifetime = ServiceLifetime.Singleton);
```

The dispatcher itself is configured the same way, for applications that need a new instance for every resolution:

```csharp
builder.Services.AddDispatcher(options =>
    options.ServiceLifetime = ServiceLifetime.Transient);
```

Which lifetimes are accepted differs by what is being registered:

- The dispatcher supports `Scoped` and `Transient`. `Singleton` is rejected, because it would capture the root service provider and could not safely resolve scoped handlers or pipeline behaviors.
- Handlers support all three Microsoft DI lifetimes.
- Pipeline behaviors are configured independently of both, through their own registration methods.

`DispatcherOptions` and `DispatcherTelemetryOptions` are in the `Dispatcher` namespace and ship in the core `Send0xx.Dispatcher` package.

## Messages and handlers

There are three kinds of message, each with its own contracts:

| Kind | Message implements | Handler implements |
| --- | --- | --- |
| Query | `IQuery<TResponse>` | `IQueryHandler<TQuery, TResponse>` |
| Command | `ICommand` or `ICommand<TResponse>` | `ICommandHandler<TCommand>` or `ICommandHandler<TCommand, TResponse>` |
| Notification | `INotification` | `INotificationHandler<TNotification>` |

All dispatchable messages implement `IMessage`. Queries and commands additionally implement `IRequest`
through their `IQueryBase` and `ICommandBase` family markers, while notifications implement `IMessage`
directly.

### Queries

A query always has a response:

```csharp
public sealed record GetOrderQuery(Guid Id) : IQuery<Order?>;

internal sealed class GetOrderQueryHandler(OrderStore store)
    : IQueryHandler<GetOrderQuery, Order?>
{
    public ValueTask<Order?> HandleAsync(
        GetOrderQuery query,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(store.Find(query.Id));
}
```

Dispatch it with `QueryAsync`:

```csharp
var order = await queries.QueryAsync(new GetOrderQuery(id), cancellationToken);
```

### Commands

A command may return a response:

```csharp
public sealed record CreateOrderCommand(string ProductId, int Quantity)
    : ICommand<Guid>;

internal sealed class CreateOrderCommandHandler
    : ICommandHandler<CreateOrderCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Guid.NewGuid());
}
```

Or it may be resultless:

```csharp
public sealed record ClearOrdersCommand : ICommand;

internal sealed class ClearOrdersCommandHandler
    : ICommandHandler<ClearOrdersCommand>
{
    public ValueTask HandleAsync(
        ClearOrdersCommand command,
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
```

Execute both forms with `ExecuteAsync`:

```csharp
var orderId = await commands.ExecuteAsync(
    new CreateOrderCommand("keyboard", 2),
    cancellationToken);

await commands.ExecuteAsync(new ClearOrdersCommand(), cancellationToken);
```

### Notifications

Notifications can have zero or more handlers:

```csharp
public sealed record OrderCreated(Guid OrderId) : INotification;

internal sealed class RecordOrderCreated
    : INotificationHandler<OrderCreated>
{
    public ValueTask HandleAsync(
        OrderCreated notification,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Order {notification.OrderId} was created.");
        return ValueTask.CompletedTask;
    }
}
```

Publish a notification with `PublishAsync`:

```csharp
await notifications.PublishAsync(new OrderCreated(orderId), cancellationToken);
```

Notification handlers run sequentially in registration order. Publishing a notification with no handlers is a no-op.

An open generic notification handler can observe every compatible known concrete notification:

```csharp
internal sealed class AuditHandler<TNotification>
    : INotificationHandler<TNotification>
    where TNotification : INotification
{
    public ValueTask HandleAsync(
        TNotification notification,
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}

builder.Services.AddNotificationHandler(typeof(AuditHandler<>));
```

Assembly scanning and generated handler registration discover this canonical shape automatically.
Dispatcher first invokes the one selected closed notification route, then invokes compatible open
handlers in registration order, closed over the concrete published type. Open handlers are registered
as their own services and therefore do not appear in `IEnumerable<INotificationHandler<TNotification>>`;
that enumerable remains the closed-handler view.

### Routing

Queries and commands require exactly one selected handler. A missing handler throws
`HandlerNotFoundException`, and duplicate handlers for the same handled message type throw
`DuplicateHandlerException`. Notifications have no such requirement.

Routes are polymorphic and precomputed. A concrete message uses its exact handler when one exists;
otherwise Dispatcher selects the most-specific compatible base class or interface handler. For
example, `UserCreatedEvent : DomainEvent` routes to a `INotificationHandler<DomainEvent>` when no
`UserCreatedEvent` handler exists, as long as `UserCreatedEvent` is a known route target; the next
section covers how a concrete type becomes one. A message type is selected this way, and then:

- Notification dispatch invokes every registered handler for that one selected type, sequentially in
  registration order. It does not broadcast across the inheritance hierarchy, so a handler for the
  derived type and a handler for its base type never both run. Compatible open generic handlers still
  run afterwards, as described above.
- Query and command dispatch invokes the single handler for the selected type, including that type's
  pipeline behaviors.
- Unrelated equally specific candidates make the route ambiguous, which fails during registry
  creation or source generation rather than at dispatch.

A fallback route is precomputed only for concrete message types Dispatcher knows about, its route
targets. Reflection assembly scanning discovers route targets declared in handler assemblies and in
the assemblies that declare their handled message types. Source generation uses the same assemblies,
plus concrete messages declared by the generated host. This supports shared contracts assemblies
without scanning every application and framework reference.

The reflection implementation cannot discover a derived message declared in an otherwise unrelated
assembly. When handlers are registered only through the typed registration methods, or when a derived
type lives outside the discovered assemblies, explicitly register each concrete type that needs a
precomputed fallback route:

```csharp
builder.Services
    .AddQueryHandler<BaseQuery, Result, BaseQueryHandler>()
    .AddDispatcherMessage<DerivedQuery>();
```

Without that route target the derived type has no fallback route, and the base handler is never reached:
dispatching `DerivedQuery` throws `HandlerNotFoundException`, and publishing a derived notification
finds no handler and does nothing.

`AddDispatcherMessage` applies to the reflection-based implementation only. Source-generated routes
must be known at build time from the generated host, a generated handler module, or an assembly that
declares one of the handled message types.

## Pipeline behaviors

Pipeline behaviors apply cross-cutting work around queries and commands:

```csharp
internal sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Executing {typeof(TRequest).Name}");
        return await next(cancellationToken);
    }
}
```

Register an open generic behavior:

```csharp
builder.Services.AddPipelineBehavior(typeof(LoggingBehavior<,>));
```

The first registered behavior is the outermost. Behaviors may short-circuit by returning without calling `next`, and they may pass a replacement cancellation token to `next`.

Registering the same behavior more than once is safe: the first registration wins and later ones are ignored, so a behavior never runs twice in one pipeline.

The same `IPipelineBehavior<TRequest, TResponse>` contract handles queries and both command shapes. A resultless `ICommand` is adapted to `Unit` only inside the pipeline; its public handler and dispatch methods remain resultless.

The constraint on `TRequest` decides which requests a behavior applies to:

| Constraint | Applies to |
| --- | --- |
| `IRequest` | Every query and command |
| `IQueryBase` | Queries only |
| `ICommandBase` | Both command shapes |
| `ICommand` | Resultless commands only |
| `ICommand<TResponse>` | Response-bearing commands only |

## OpenTelemetry

Dispatcher can emit tracing activities and an operation-duration histogram through the
built-in .NET diagnostics APIs. Both signals are disabled by default, and disabled
telemetry adds no work to the dispatch path.

Enable either signal through `DispatcherOptions.Telemetry`:

```csharp
builder.Services.AddDispatcher(options =>
{
    options.Telemetry.EnableTracing = true;
    options.Telemetry.EnableMetrics = true;
});
```

The default activity source and meter name is `Dispatcher`. Register those names with
the application's OpenTelemetry providers:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Dispatcher"))
    .WithMetrics(metrics => metrics.AddMeter("Dispatcher"));
```

Use `ActivitySourceName` and `MeterName` to choose application-specific names; pass the
same values to `AddSource` and `AddMeter`. Dispatcher itself does not depend on the
OpenTelemetry SDK or an exporter.

Telemetry surrounds the complete routed operation, outside user pipeline behaviors and
around all sequential notification handlers. Missing query or command handlers and
notifications with no handlers do not emit telemetry. Failures, including cancellation,
set `error.type`, mark the activity as an error, and add a standard exception event.

The emitted histogram is `dispatcher.operation.duration`, measured in seconds. Traces
and metrics include `dispatcher.operation.name`, `dispatcher.message.type`, and
`dispatcher.message.kind`.

## Source generation and Native AOT

`Send0xx.Dispatcher.SourceGeneration` generates typed handler registrations and a dispatcher implementation. Reflection is not used for registration or dispatch, and the package does not reference the reflection-based Dispatcher implementation.

In a single-project application, opt in at assembly level and give the generated extension methods unique names:

```csharp
using Dispatcher;
using Dispatcher.SourceGeneration;

[assembly: GenerateDispatcherHandlers("AddApplicationHandlers")]
[assembly: GenerateDispatcher("AddDispatcher")]
```

Register the generated dispatcher and handlers:

```csharp
builder.Services
    .AddDispatcher()
    .AddApplicationHandlers();
```

The generated `AddDispatcher` method accepts the same lifetime options:

```csharp
builder.Services
    .AddDispatcher(options =>
        options.ServiceLifetime = ServiceLifetime.Transient)
    .AddApplicationHandlers(options =>
        options.ServiceLifetime = ServiceLifetime.Singleton);
```

Dispatcher and generated handler lifetimes are configured independently; the example uses a transient dispatcher and singleton handlers.

Handlers may remain internal. The generator discovers queries, commands, notifications, and pipeline behaviors at compile time and emits explicit DI registrations and frozen dispatch tables.

For applications split across assemblies, each referenced assembly can generate its own handler-registration method while the host generates the dispatcher:

```csharp
// In a referenced handlers assembly
using Dispatcher.SourceGeneration;

[assembly: GenerateDispatcherHandlers("AddOrderHandlers")]
```

```csharp
// In the host assembly
using Dispatcher.SourceGeneration;

[assembly: GenerateDispatcher("AddDispatcher")]

builder.Services
    .AddDispatcher()
    .AddOrderHandlers();
```

Modular composition is supported, but it is not required. See the Native AOT sample for a complete application in which the host composes internal handlers from two referenced assemblies.

## Samples

All samples target .NET 10. Start with the [samples overview](samples/README.md), or go directly to one of these applications:

- [Dependency-injection Minimal API](samples/DependencyInjection/Dispatcher.SampleApi) demonstrates shared contracts, reflection-based handler scanning, polymorphic routes, FluentValidation pipeline behavior, and internal handlers in Orders and Stock assemblies. Run it with:

  ```bash
  dotnet run --project samples/DependencyInjection/Dispatcher.SampleApi
  ```

- [Native AOT Minimal API](samples/NativeAot/Dispatcher.NativeAotHostSample) demonstrates shared contracts, generated polymorphic routes, a host-generated dispatcher, open generic notification handling and logging behavior, source-generated JSON metadata, and internal handlers composed from two referenced assemblies. Publish it with:

  ```bash
  dotnet publish samples/NativeAot/Dispatcher.NativeAotHostSample -c Release
  ```

## Packages

The implementation packages have parallel dependency paths:

```text
Send0xx.Dispatcher.SourceGeneration    ─┐
                                        ├─> Send0xx.Dispatcher ─> Send0xx.Dispatcher.Abstractions
Send0xx.Dispatcher.DependencyInjection ─┘
```

- `Send0xx.Dispatcher.Abstractions` contains messages, handlers, pipeline contracts, dispatcher interfaces, and `Unit`.
- `Send0xx.Dispatcher` contains typed Microsoft DI registration extensions and Dispatcher and telemetry options. It references Microsoft DI abstractions because these public APIs use Microsoft DI types.
- `Send0xx.Dispatcher.DependencyInjection` contains the reflection-based Dispatcher implementation, registry, wrappers, handler scanning, pipelines, and telemetry runtime.
- `Send0xx.Dispatcher.SourceGeneration` contains the source-generation analyzer and references the shared Dispatcher runtime APIs for trimming and Native AOT.

Most applications should reference either `Send0xx.Dispatcher.DependencyInjection` or `Send0xx.Dispatcher.SourceGeneration`, not every package individually. Package IDs use the `Send0xx` prefix. Core APIs remain in the `Dispatcher` namespace, while generator opt-in attributes and generated dispatcher registration extensions use `Dispatcher.SourceGeneration`.

## Performance

The direct handler path avoids pipeline construction when no behavior applies. The runtime resolves behaviors for every dispatch so scoped and transient lifetimes remain correct, and notification handlers execute without a reflection-based dispatch path.

Run the .NET 10 BenchmarkDotNet suite in Release mode:

```bash
dotnet run --project benchmarks/Dispatcher.Benchmarks -c Release
```

The [benchmark notes](benchmarks/Dispatcher.Benchmarks/README.md) describe the available latency, allocation, pipeline, and implementation comparisons.

## Current limitations

- Notifications execute sequentially rather than concurrently.
- Pipeline behaviors apply to queries and commands, not notifications.
- Registering a handler or behavior through a factory delegate is not recommended when it is also covered by scanning or a typed registration method. Duplicate registrations are otherwise detected and ignored, but Microsoft DI does not expose the type a factory returns, so a factory registration cannot be matched against another one. Both survive: a notification handler fires twice per publish, and a pipeline behavior runs twice per request. A query or command instead fails fast, because it allows only one handler: the duplicate throws `DuplicateHandlerException` when the registry is created. Register such handlers and behaviors by type or as an instance instead.

## Project

Contributions and design discussions are welcome. See the [contribution guide](https://github.com/send0xx/Dispatcher/blob/main/CONTRIBUTING.md) to get started. Maintainers can find CI and NuGet publishing instructions in the [release guide](https://github.com/send0xx/Dispatcher/blob/main/docs/RELEASING.md).

Dispatcher is licensed under the [MIT License](https://github.com/send0xx/Dispatcher/blob/main/LICENSE).
