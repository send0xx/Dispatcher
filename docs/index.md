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
