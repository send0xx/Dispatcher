# Dispatcher

Dispatcher is a small CQRS library for .NET applications that use dependency injection. It supports queries, result-bearing and resultless commands, notifications, and ordered pipeline behaviors. Handler lookup uses `FrozenDictionary`; reflection is limited to handler registration at startup.

The libraries target .NET 8 and .NET 10. The example application targets .NET 10.

## Packages

- `Dispatcher.Abstractions` contains messages, handlers, behaviors, and dispatch interfaces.
- `Dispatcher` contains the container-neutral runtime.
- `Dispatcher.Extensions.Microsoft.DependencyInjection` provides typed, reflection-free Microsoft DI registrations.
- `Dispatcher.DependencyInjection` provides the reflection-based Microsoft DI implementation.
- `Dispatcher.SourceGeneration` generates a dispatcher and handler registrations without reflection.

Choose one implementation package. Use the Microsoft DI extension for the reflection-based runtime,
or use source generation for a generated dispatcher:

```bash
dotnet add package Dispatcher.DependencyInjection --version 1.0.0-preview.2
# or
dotnet add package Dispatcher.SourceGeneration --version 1.0.0-preview.2
```

## Define a query

```csharp
public sealed record GetOrderQuery(Guid Id) : IQuery<Order?>;

internal sealed class GetOrderQueryHandler(OrderStore store)
    : IQueryHandler<GetOrderQuery, Order?>
{
    public ValueTask<Order?> HandleAsync(
        GetOrderQuery query,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Find(query.Id));
}
```

## Define commands

```csharp
public sealed record CreateOrderCommand(string ProductId, int Quantity)
    : ICommand<Guid>;

internal sealed class CreateOrderCommandHandler
    : ICommandHandler<CreateOrderCommand, Guid>
{
    public ValueTask<Guid> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(Guid.NewGuid());
}

public sealed record ClearOrdersCommand : ICommand;

internal sealed class ClearOrdersCommandHandler
    : ICommandHandler<ClearOrdersCommand>
{
    public ValueTask HandleAsync(
        ClearOrdersCommand command,
        CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
```

## Register Dispatcher and module handlers

Register infrastructure once in the application:

```csharp
services.AddDispatcher();
services.AddOrdersModule();
services.AddStockModule();
```

Each module registers its own assembly. Reflection includes internal handler types:

```csharp
public static IServiceCollection AddOrdersModule(this IServiceCollection services)
{
    services.AddSingleton<OrderStore>();
    return services.AddDispatcherHandlers<OrdersModuleMarker>();
}
```

`AddDispatcher()` never scans assemblies implicitly. Handler registration can occur before or after it, and registering the same assembly again is ignored.

The dispatcher, handlers, and behaviors are scoped by default. Resolve and use dispatcher interfaces inside a DI scope, as ASP.NET Core does for each request. Avoid overriding the dispatcher with a singleton registration because scoped handlers and behaviors must be resolved from their owning scope.

For trimming and Native AOT, reference only `Dispatcher.SourceGeneration`. Each module opts into
handler registration, while the host opts into the single dispatcher:

```csharp
[assembly: GenerateDispatcherHandlers("AddOrdersHandlers")]

[assembly: GenerateDispatcher("AddDispatcher")]
```

```csharp
services.AddOrdersHandlers().AddStockHandlers().AddDispatcher();
```

Each module generates registrations inside its own assembly, allowing its handlers to remain
internal. The host generator discovers opted-in referenced modules and emits one internal
`Dispatcher` with routes for all of them.
It uses frozen dispatch tables while resolving handlers and behaviors from the current service-provider
scope. Generated handler registration uses the typed methods from
`Dispatcher.Extensions.Microsoft.DependencyInjection` and does not reference the reflection-based
`Dispatcher.DependencyInjection` package.

## Dispatch messages

```csharp
var order = await queries.QueryAsync(new GetOrderQuery(id), cancellationToken);
var orderId = await commands.ExecuteAsync(
    new CreateOrderCommand("keyboard", 2), cancellationToken);
await commands.ExecuteAsync(new ClearOrdersCommand(), cancellationToken);
await publisher.PublishAsync(new OrderCreated(orderId), cancellationToken);
```

Queries and commands require exactly one handler. A missing handler throws `HandlerNotFoundException`; duplicate handlers throw `DuplicateHandlerException`. Notifications allow multiple handlers, run sequentially in registration order, and are a no-op when no handler exists.

Dispatch uses exact concrete message types. Polymorphic routing is not included in this version.

## Pipeline behaviors

Implement the behavior contract matching the message shape and call `next` to continue:

```csharp
internal sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Executing {typeof(TRequest).Name}");
        return await next(cancellationToken);
    }
}

services.AddPipelineBehavior(typeof(LoggingBehavior<,>));
```

The same `IPipelineBehavior<TRequest, TResponse>` contract applies to queries and every command. A resultless `ICommand` is represented as `ICommand<Unit>` inside the pipeline, while its handler and `ExecuteAsync` overload remain resultless for normal application code. The first registered behavior is outermost, and a behavior may short-circuit by returning without calling `next`.

## Sample

Run the beginner-friendly Minimal API:

```bash
dotnet run --project samples/DependencyInjection/Dispatcher.SampleApi
```

The sample uses internal handlers in separate Orders and Stock modules. Its FluentValidation command behavior returns HTTP 400 validation problems and its `OrderCreated` notification reserves stock across module boundaries. See [the sample walkthrough](samples/DependencyInjection/Dispatcher.SampleApi/README.md).

The [Native AOT sample](samples/NativeAot/Dispatcher.NativeAotHostSample) demonstrates
two referenced modules with internal handlers composed into one host-generated dispatcher.

## Benchmarks

Run the .NET 10 BenchmarkDotNet suite in Release mode:

```bash
dotnet run --project benchmarks/Dispatcher.Benchmarks -c Release
```

It reports latency and managed allocations for each message shape and for pipelines
containing zero, one, or three behaviors. See [the benchmark notes](benchmarks/Dispatcher.Benchmarks/README.md).

## Current limitations

Reflection-based handler and behavior registration is not trimming or Native AOT safe. Typed and generated handler registration and typed closed behavior registration are AOT compatible.

Future maintenance notes are available in the [v1 implementation plan](docs/PLAN.md) and [Native AOT roadmap](docs/AOT.md).
