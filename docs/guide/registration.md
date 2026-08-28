---
uid: guide.registration
title: Registration and lifetimes
description: Register handlers by scanning or explicitly, and control dispatcher and handler service lifetimes.
---

# Registration and lifetimes

Dispatcher and handlers are **scoped by default**. Resolve them inside a DI scope, as ASP.NET Core
does for each request.

## Scanning an assembly

The usual setup registers infrastructure and then scans one or more assemblies:

```csharp
builder.Services
    .AddDispatcher()
    .AddDispatcherHandlers(typeof(Program).Assembly);
```

`AddDispatcher()` registers infrastructure only and never scans assemblies implicitly.
`AddDispatcherHandlers()` registers internal handler classes. Registering the same assembly more than
once is safe.

## Registering a single handler

A single handler can be registered explicitly instead of scanning for it:

```csharp
using Dispatcher;

builder.Services.AddQueryHandler<GetGreetingQuery, string, GetGreetingQueryHandler>();
```

There is one method per handler kind: `AddQueryHandler`, `AddCommandHandler`, and
`AddNotificationHandler`.

> [!NOTE]
> Registering handlers only through the typed methods means Dispatcher may not know about derived
> message types. See [route targets](routing.md#route-targets).

## Setting lifetimes

Each registration method takes an optional delegate that sets the handler lifetime, and assembly
scanning takes the same delegate:

```csharp
builder.Services.AddQueryHandler<GetGreetingQuery, string, GetGreetingQueryHandler>(options =>
    options.ServiceLifetime = ServiceLifetime.Singleton);

builder.Services.AddDispatcherHandlers(
    typeof(Program).Assembly,
    options => options.ServiceLifetime = ServiceLifetime.Singleton);
```

The dispatcher itself is configured the same way, for applications that need a new instance for every
resolution:

```csharp
builder.Services.AddDispatcher(options =>
    options.ServiceLifetime = ServiceLifetime.Transient);
```

## Which lifetimes are accepted

Which lifetimes are accepted differs by what is being registered:

| Registered | `Scoped` | `Transient` | `Singleton` |
| --- | :---: | :---: | :---: |
| Dispatcher | ✅ | ✅ | ❌ rejected |
| Handlers | ✅ | ✅ | ✅ |

> [!IMPORTANT]
> A singleton dispatcher is rejected because it would capture the root service provider and could not
> safely resolve scoped handlers or pipeline behaviors.

[Pipeline behaviors](pipeline-behaviors.md) are configured independently of both, through their own
registration methods.

`DispatcherOptions` and `DispatcherTelemetryOptions` are in the `Dispatcher` namespace and ship in the
core `Send0xx.Dispatcher` package.
