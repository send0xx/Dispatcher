---
uid: guide.getting-started
title: Getting started
description: Install Dispatcher, write a query handler, and dispatch your first message.
---

# Getting started

## Install

For the simplest Microsoft dependency-injection setup:

```bash
dotnet add package Send0xx.Dispatcher.DependencyInjection --version 2.0.0
```

For source-generated registration and Native AOT, install instead:

```bash
dotnet add package Send0xx.Dispatcher.SourceGeneration --version 2.0.0
```

> [!IMPORTANT]
> Choose **one** implementation package. Both bring in the abstractions, runtime, and Microsoft DI
> integration they require. [Source generation](source-generation.md) compares the two modes.

The rest of this page uses the reflection-based package.

## Define a query and its handler

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

The handler is `internal`. Handlers never have to be public, under either registration mode.

## Register Dispatcher

```csharp
using Dispatcher.DependencyInjection;

builder.Services
    .AddDispatcher()
    .AddDispatcherHandlers(typeof(Program).Assembly);
```

`AddDispatcher()` registers infrastructure only and never scans assemblies implicitly.
`AddDispatcherHandlers()` registers internal handler classes. Registering the same assembly more than
once is safe.

## Dispatch it

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

Request `/greetings/world` to get `"Hello, world!"`.

## Dispatcher interfaces

Inject the narrowest one a class needs. They all resolve to the same implementation, so a narrow
interface costs nothing at runtime.

| Interface | Method | Dispatches |
| --- | --- | --- |
| `IQueryDispatcher` | `QueryAsync` | Queries |
| `ICommandDispatcher` | `ExecuteAsync` | Commands, with or without a response |
| `INotificationDispatcher` | `PublishAsync` | Notifications |
| `IDispatcher` | all of the above | Anything, for classes that need more than one kind |
