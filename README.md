# Dispatcher

Dispatcher is a small CQRS library for .NET applications that use dependency injection. It supports queries, result-bearing and resultless commands, notifications, and ordered pipeline behaviors. Handler lookup uses `FrozenDictionary`; reflection is limited to handler registration at startup.

The libraries target .NET 8 and .NET 10. The example application targets .NET 10.

## Packages

- `Dispatcher.Abstractions` contains messages, handlers, behaviors, and dispatch interfaces.
- `Dispatcher` contains the container-neutral runtime.
- `Dispatcher.Extensions.DependencyInjection` adds Microsoft DI registration and references the other packages transitively.

Most applications only need to install `Dispatcher.Extensions.DependencyInjection`.

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
        CancellationToken cancellationToken)
    {
        // Execute the command and return its result.
    }
}

public sealed record ClearOrdersCommand : ICommand;

internal sealed class ClearOrdersCommandHandler
    : ICommandHandler<ClearOrdersCommand>
{
    public ValueTask HandleAsync(
        ClearOrdersCommand command,
        CancellationToken cancellationToken)
    {
        // Execute a command without a result.
    }
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
dotnet run --project samples/Dispatcher.SampleApi
```

The sample uses internal handlers in separate Orders and Stock modules. Its FluentValidation command behavior returns HTTP 400 validation problems and its `OrderCreated` notification reserves stock across module boundaries. See [the sample walkthrough](samples/Dispatcher.SampleApi/README.md).

## Benchmarks

Run the .NET 10 BenchmarkDotNet suite in Release mode:

```bash
dotnet run --project benchmarks/Dispatcher.Benchmarks -c Release
```

It reports latency and managed allocations for each message shape and for pipelines
containing zero, one, or three behaviors. See [the benchmark notes](benchmarks/Dispatcher.Benchmarks/README.md).

## Current limitations

Handler registration uses reflection and is not trimming or NativeAOT safe. The separate `AddDispatcherHandlers` module-registration seam is intended to support generated registrations in a future release.
