---
uid: guide.source-generation
title: Source generation
description: Compile-time handler registration and dispatch for trimming and Native AOT.
---

# Source generation

Dispatcher ships two registration and dispatch implementations behind the same public API. Your
messages, handlers, and pipeline behaviors are written identically in both. What changes is how
handlers are discovered and when route errors surface.

| | Reflection | Source generation |
| --- | --- | --- |
| Package | `Send0xx.Dispatcher.DependencyInjection` | `Send0xx.Dispatcher.SourceGeneration` |
| Handlers discovered | At startup, by scanning assemblies | At compile time |
| Opt-in | `AddDispatcherHandlers(assembly)` | `[assembly: GenerateDispatcherHandlers("…")]` |
| Trimming / Native AOT | Not supported | Supported |
| Route errors surface | At startup | At build time |
| Manual route target | `AddDispatcherMessage<T>()` | Not applicable |

> [!TIP]
> If you publish with Native AOT or trimming, use source generation. Otherwise start with reflection
> and switch later: it is a package swap plus two assembly attributes, and your handlers do not change.

`Send0xx.Dispatcher.SourceGeneration` generates typed handler registrations and a dispatcher
implementation. Reflection is not used for registration or dispatch, and the package does not
reference the reflection-based implementation at all, so there is no reflective code path for the
trimmer to preserve.

## Single-project setup

Opt in at assembly level and give the generated extension methods unique names:

```csharp
using Dispatcher;
using Dispatcher.SourceGeneration;

[assembly: GenerateDispatcherHandlers("AddApplicationHandlers")]
[assembly: GenerateDispatcher("AddDispatcher")]
```

```csharp
builder.Services
    .AddDispatcher()
    .AddApplicationHandlers();
```

Handlers may remain internal. The generator discovers queries, commands, notifications, and pipeline
behaviors at compile time and emits explicit DI registrations and frozen dispatch tables.

## Lifetimes

The generated `AddDispatcher` accepts the same options as the reflection-based one, and the
[same lifetime rules](registration.md#which-lifetimes-are-accepted) apply:

```csharp
builder.Services
    .AddDispatcher(options =>
        options.ServiceLifetime = ServiceLifetime.Transient)
    .AddApplicationHandlers(options =>
        options.ServiceLifetime = ServiceLifetime.Singleton);
```

Dispatcher and generated handler lifetimes are configured independently.

## Multi-assembly composition

Each referenced assembly can generate its own handler-registration method while the host generates the
dispatcher:

```csharp
// In a referenced handlers assembly
[assembly: GenerateDispatcherHandlers("AddOrderHandlers")]
```

```csharp
// In the host assembly
[assembly: GenerateDispatcher("AddDispatcher")]

builder.Services
    .AddDispatcher()
    .AddOrderHandlers();
```

Modular composition is supported, but not required.

## Routes must be known at build time

> [!IMPORTANT]
> Source-generated routes must be known at build time from the generated host, a generated handler
> module, or an assembly that declares one of the handled message types. There is no equivalent of
> `AddDispatcherMessage<T>()`.

See [route targets](routing.md#route-targets).
