---
uid: api.index
title: API reference
description: Generated API reference for the Dispatcher packages.
---

# API reference

This section is generated from the XML documentation comments in the source, so it always matches the
shipped release.

## Namespaces

| Namespace | What lives there |
| --- | --- |
| `Dispatcher` | Message and handler contracts, dispatcher interfaces, `Unit`, `DispatcherOptions`, `DispatcherTelemetryOptions`, and the typed DI registration extensions |
| `Dispatcher.DependencyInjection` | The reflection-based `Dispatcher` implementation and its registration extensions |

## Frequently used types

**Dispatching**

- `IDispatcher`, `IQueryDispatcher`, `ICommandDispatcher`, `INotificationDispatcher`

**Messages**

- `IMessage`, `IRequest`, `IQueryBase`, `ICommandBase`
- `IQuery<TResponse>`, `ICommand`, `ICommand<TResponse>`, `INotification`

**Handlers and pipeline**

- `IQueryHandler<,>`, `ICommandHandler<>`, `ICommandHandler<,>`, `INotificationHandler<>`
- `IPipelineBehavior<TRequest, TResponse>`, `RequestHandlerDelegate<TResponse>`, `Unit`

**Configuration**

- `ServiceCollectionExtensions`, `DispatcherOptions`, `DispatcherTelemetryOptions`

**Failures**

| Exception | Thrown when |
| --- | --- |
| `HandlerNotFoundException` | A query or command has no compatible handler |
| `DuplicateHandlerException` | Two handlers are registered for the same handled message type |
| `AmbiguousHandlerException` | Two unrelated equally specific candidate handlers match a message |
| `UnsupportedHandlerException` | A scanned type implements a handler contract in an unsupported shape |
| `AssemblyScanException` | An assembly cannot be scanned for handlers |

> [!NOTE]
> These exception types ship in `Send0xx.Dispatcher.DependencyInjection` and belong to the
> reflection-based implementation. In source-generation mode the equivalent problems are reported as
> compiler diagnostics at build time.

## What is not listed here

The `GenerateDispatcher` and `GenerateDispatcherHandlers` attributes do **not** appear in this
reference. The source generator injects them into your own compilation as internal types rather than
shipping them as public API, so there is no assembly for DocFX to read them from. The generated
registration extension methods are likewise emitted into your project. Both are documented in
[Source generation](../guide/source-generation.md).

> [!TIP]
> Looking for how these types fit together rather than what they are? Start with
> [Getting started](../guide/getting-started.md).
