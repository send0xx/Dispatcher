# Dispatcher

[![NuGet DependencyInjection](https://img.shields.io/nuget/v/Send0xx.Dispatcher.DependencyInjection.svg?label=DependencyInjection)](https://www.nuget.org/packages/Send0xx.Dispatcher.DependencyInjection/)
[![NuGet SourceGeneration](https://img.shields.io/nuget/v/Send0xx.Dispatcher.SourceGeneration.svg?label=SourceGeneration)](https://www.nuget.org/packages/Send0xx.Dispatcher.SourceGeneration/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/send0xx/Dispatcher/blob/main/LICENSE)
[![CI](https://github.com/send0xx/Dispatcher/actions/workflows/ci.yml/badge.svg)](https://github.com/send0xx/Dispatcher/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/send0xx/Dispatcher/graph/badge.svg)](https://codecov.io/gh/send0xx/Dispatcher)

Dispatcher is a small CQRS library for .NET applications that use dependency injection. It provides focused APIs for queries, commands, notifications, handlers, and pipeline behaviors.

**[Read the documentation](https://send0xx.github.io/Dispatcher/)**

## Main features

- **Reflection-free dispatch.** Handler routes are stored in frozen dictionaries, and handlers and pipeline behaviors are resolved from the current dependency-injection scope.
- **Polymorphic routes.** A message uses its exact handler when one exists, and otherwise the most-specific compatible base class or interface handler. Routes are decided during registry creation or at compile time, not per dispatch.
- **Internal handlers and modular composition.** Handlers never have to be public, and an application split across assemblies registers each module's handlers separately, under either registration mode.
- **Trimming and Native AOT support.** The source generator emits registrations and dispatch tables at compile time, and its package does not reference the reflection-based implementation at all.
- **Built-in OpenTelemetry.** Tracing activities and an operation-duration histogram, disabled by default, adding no work to the dispatch path while off.

## Install

For the simplest Microsoft dependency-injection setup, install:

```bash
dotnet add package Send0xx.Dispatcher.DependencyInjection
```

For source-generated registration and Native AOT, install instead:

```bash
dotnet add package Send0xx.Dispatcher.SourceGeneration
```

Choose one implementation package. Both bring in the abstractions, runtime, and dependency injection integration they require.

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

The [getting started guide](https://send0xx.github.io/Dispatcher/guide/getting-started.html) walks through this in full.

## Samples

All samples target .NET 10. Start with the [samples overview](https://github.com/send0xx/Dispatcher/blob/main/samples/README.md), or go directly to the [dependency-injection Minimal API](https://github.com/send0xx/Dispatcher/tree/main/samples/DependencyInjection/Dispatcher.SampleApi) or the [Native AOT Minimal API](https://github.com/send0xx/Dispatcher/tree/main/samples/NativeAot/Dispatcher.NativeAotHostSample).

## Contributing

Contributions and design discussions are welcome. See the [contribution guide](https://github.com/send0xx/Dispatcher/blob/main/CONTRIBUTING.md) to get started. Maintainers can find CI, documentation, and NuGet publishing instructions in the [release guide](https://github.com/send0xx/Dispatcher/blob/main/RELEASING.md).

## License

Dispatcher is licensed under the [MIT License](https://github.com/send0xx/Dispatcher/blob/main/LICENSE).
