# Open generic notification handlers

## Status

Future design proposal. Open generic notification handlers are not currently supported.
This document records the intended behavior and the questions to settle before implementation.

## Goal

Allow one notification handler definition to observe every compatible concrete notification:

```csharp
public sealed class AuditHandler<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification
{
    public ValueTask HandleAsync(
        TNotification notification,
        CancellationToken cancellationToken = default)
    {
        // Audit the concrete notification.
        return ValueTask.CompletedTask;
    }
}
```

This feature is useful for cross-cutting notification concerns such as auditing, validation,
diagnostics, or outbox integration. It should apply only to notifications. Queries and commands
must retain their exactly-one-selected-handler semantics.

## Proposed dispatch semantics

Open generic handlers are additive observers, not polymorphic routing candidates.

For a published notification, Dispatcher should:

1. Select the normal closed route: the exact handled notification type when available,
   otherwise the most-specific compatible handled base class or interface.
2. Invoke the closed handlers on that one selected route.
3. Invoke each compatible open generic handler closed over the concrete published type.

Given `UserCreatedEvent : DomainEvent` and `AuditHandler<TNotification>`, the expected behavior is:

| Closed handlers | Published type | Invoked handlers |
| --- | --- | --- |
| `DomainEventHandler` | `UserCreatedEvent` | `DomainEventHandler`, then `AuditHandler<UserCreatedEvent>` |
| `DomainEventHandler`, `UserCreatedEventHandler` | `UserCreatedEvent` | `UserCreatedEventHandler`, then `AuditHandler<UserCreatedEvent>` |
| None | `UserCreatedEvent` | `AuditHandler<UserCreatedEvent>`, when `UserCreatedEvent` is a known route target |

The exact closed handler continues to suppress closed base handlers. The generic handler does not
make the closed route ambiguous, suppress a closed handler, or cause notification broadcasting
across multiple hierarchy levels.

The generic handler must be closed over the runtime notification type. Closing it over the
selected base route would lose type-specific behavior and would make exact and polymorphic routes
behave differently.

## Supported handler shape

The first implementation should accept only the canonical form:

```csharp
Handler<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification
```

The handler must:

- Be a non-abstract class with one generic type parameter.
- Implement `INotificationHandler<TNotification>` using that parameter directly.
- Expose a constructor that the relevant registration path can activate.
- Be closed only for concrete notification types that satisfy all generic constraints.

More complex mappings such as `INotificationHandler<Envelope<T>>`, partially open handlers, and
handlers with unrelated generic parameters should remain unsupported initially. The reflection
scanner and source generator should report invalid shapes consistently.

## Registration and discovery

Assembly scanning should discover canonical open generic notification handlers. Explicit
registration should also be possible, likely through an overload shaped like:

```csharp
services.AddNotificationHandler(typeof(AuditHandler<>));
```

The exact public API should be decided during implementation after checking whether the existing
registration metadata can represent the responsibility cleanly. A new public metadata type should
be introduced only if composing `MessageRegistration` and the existing handler registrations
cannot express the feature without ambiguity.

Startup-precomputed routes require a finite set of concrete notification types. A generic handler
does not identify those types by itself. The implementation therefore needs to define how a
notification with no closed handler becomes a known route target. Prefer notification types
already discovered from registered modules or emitted as explicit message metadata; do not scan
every referenced application assembly implicitly.

Source generation should discover the same handler shape and pre-close registrations for every
compatible concrete notification known to the generated host. Generated dispatch must not use
runtime reflection, `MakeGenericType`, or a dispatch-time cache.

## Ordering

The proposed deterministic order is:

1. Closed handlers from the selected route, in their existing registration order.
2. Compatible open generic handlers, in their registration order.

This preserves current notification ordering within the selected route and avoids pretending that
closed and open registrations have one global order when they may come from different modules.
Confirm this ordering before implementation because it becomes observable API behavior.

## Current behavior and implementation risk

Current support is intentionally incomplete:

- The reflection scanner excludes handler classes with unbound generic parameters.
- The source generator reports `DSPG003` for an open generic handler.
- The typed `AddNotificationHandler<TNotification, THandler>` API accepts only a closed handler.

A consumer can directly register an open generic Microsoft DI descriptor today. That can appear to
work when the selected route is the exact notification type because the wrapper resolves
`IEnumerable<INotificationHandler<TNotification>>`. It is not supported behavior. When Dispatcher
selects a base route, the same wrapper resolves the generic handler closed over the base type rather
than the concrete published type. A generic-only registration also does not create Dispatcher route
metadata.

The implementation must avoid invoking an open handler twice when Microsoft DI includes it in the
same enumerable as exact closed handlers. It will likely need to keep closed-route resolution and
concrete generic-handler resolution distinct while preserving configured handler lifetimes. This
should be designed using the existing registry, wrapper, and registration responsibilities before
adding another abstraction.

## Telemetry and failure behavior

Telemetry should wrap the combined operation: selected closed handlers followed by open generic
handlers. It should continue reporting the concrete published message type. The first exception or
cancellation should stop sequential execution, be recorded once for the publish operation, and be
re-thrown unchanged.

## Verification plan

Tests should cover both reflection and source-generated dispatch:

- An exact closed handler plus an open generic handler.
- A selected base handler plus an open generic handler closed over the concrete type.
- Exact closed handlers suppressing closed base handlers while the open handler still runs.
- A known notification routed only to open generic handlers.
- Multiple open handlers and deterministic sequential ordering.
- Compatible and incompatible generic constraints.
- Scoped and transient handler lifetimes and registration idempotence.
- Exceptions, cancellation, and telemetry around the combined operation.
- Cross-assembly contracts, closed handlers, and open handlers.
- Reflection and generated behavior parity, including Native AOT publishing.

Before accepting the feature, benchmark startup, generated source size, compile time, and publish
latency with thousands of notification types. Source generation can multiply the number of emitted
registrations by `concrete notification count × open generic handler count`.

## Non-goals

- Open generic query or command handlers.
- Broadcasting closed notification handlers across all compatible hierarchy levels.
- Dispatch-time reflection or runtime route caching.
- Implicit scanning of every referenced assembly.
