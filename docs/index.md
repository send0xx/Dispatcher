---
_layout: landing
title: Dispatcher
description: A small, reflection-free CQRS library for .NET applications that use dependency injection.
---

<div class="hero">

# Dispatcher

**A small CQRS library for .NET applications that use dependency injection.**

Focused APIs for queries, commands, notifications, handlers, and pipeline behaviors, with a source
generator that makes the whole dispatch path trimming- and Native AOT-friendly.

<div class="hero-actions">

[Get started](guide/getting-started.md)
[Browse the API](api/index.md)

</div>

</div>

```csharp
public sealed record GetGreetingQuery(string Name) : IQuery<string>;

internal sealed class GetGreetingQueryHandler
    : IQueryHandler<GetGreetingQuery, string>
{
    public ValueTask<string> HandleAsync(
        GetGreetingQuery query,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult($"Hello, {query.Name}!");
}

// Program.cs
builder.Services
    .AddDispatcher()
    .AddDispatcherHandlers(typeof(Program).Assembly);
```

## The three message kinds

| Kind | Purpose | Handlers | Returns |
| --- | --- | --- | --- |
| **Query** | Read something | Exactly one | Always a response |
| **Command** | Change something | Exactly one | Optionally a response |
| **Notification** | Announce something happened | Zero or more | Never |

## Why Dispatcher?

<div class="feature-grid">

<div class="feature-card">

### Reflection-free dispatch

Routes are stored in frozen dictionaries. Handlers and behaviors are resolved from the current DI
scope, never discovered per dispatch.

</div>

<div class="feature-card">

### Polymorphic routes

A message uses its exact handler when one exists, otherwise the most-specific compatible base or
interface handler. [How routing works](guide/routing.md)

</div>

<div class="feature-card">

### Trimming and Native AOT

The source generator emits registrations and dispatch tables at compile time.
[Source generation](guide/source-generation.md)

</div>

<div class="feature-card">

### Internal handlers

Handlers never have to be public, and an application split across assemblies registers each module
separately, under either mode.

</div>

<div class="feature-card">

### Built-in OpenTelemetry

Tracing activities and an operation-duration histogram, disabled by default and free while off.
[Telemetry setup](guide/opentelemetry.md)

</div>

<div class="feature-card">

### Narrow interfaces

Inject `IQueryDispatcher`, `ICommandDispatcher`, or `INotificationDispatcher` instead of one
god-interface.

</div>

</div>
