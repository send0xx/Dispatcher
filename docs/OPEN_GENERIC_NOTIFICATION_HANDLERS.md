# Open generic notification handlers

## Status

Implemented for reflection-based assembly scanning, explicit registration, and source-generated dispatch.

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

The implementation accepts only the canonical form:

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
handlers with unrelated generic parameters are unsupported. The reflection scanner and source
generator reject invalid shapes.

An invalid shape is never skipped, because a skipped handler fails invisibly later as a
notification that never arrives or a missing request handler. The source generator reports one
diagnostic per offending type. Assembly scanning collects every offending type across all scanned
assemblies and throws a single `UnsupportedHandlerException` whose `Handlers` property maps each
handler type to the reason it cannot be registered. Scanning commits nothing to the service
collection when it fails, so a rejected assembly leaves no partial registration behind.

## Registration and discovery

Assembly scanning discovers canonical open generic notification handlers. Explicit registration uses:

```csharp
services.AddNotificationHandler(typeof(AuditHandler<>));
```

Open and closed handlers both use `NotificationHandlerRegistration`. `IsOpenGeneric` is derived from
whether `HandlerType` is a generic type definition. For an open registration, `MessageType` is the
handler's notification type parameter. Because that parameter is not concrete, it cannot become a
route target during registry construction.

The DI descriptor registers the handler definition as itself, for example
`AuditHandler<> -> AuditHandler<>`. It is intentionally not registered as
`INotificationHandler<>`: closed handler enumeration therefore remains isolated from additive open
observers without keyed services. The descriptor remains the single source of truth for lifetime.

Startup-precomputed routes require a finite set of concrete notification types. A generic handler
does not identify those types by itself. The implementation therefore needs to define how a
notification with no closed handler becomes a known route target. Prefer notification types
already discovered from registered modules or emitted as explicit message metadata; do not scan
every referenced application assembly implicitly.

Source generation discovers the same handler shape. Each module emits public matching and strongly
typed invocation helpers so internal open handlers remain usable by a generated host. The host emits
a singleton execution plan from registration metadata in runtime registration order. The plan contains
route-specific static invokers and generated closed generic references; generated dispatch does not use
runtime reflection, `MakeGenericType`, or a dispatch-time cache.

## Ordering

The deterministic order is:

1. Closed handlers from the selected route, in their existing registration order.
2. Compatible open generic handlers, in their registration order.

This preserves current notification ordering within the selected route and avoids pretending that
closed and open registrations have one global order when they may come from different modules.

## Runtime implementation

The reflection registry closes compatible handler self-service types during singleton registry
creation. Closed-route wrappers resolve only `IEnumerable<INotificationHandler<TSelected>>`; combined
wrappers then resolve each preclosed open handler type and invoke it as
`INotificationHandler<TConcrete>`. Reflection and generic construction remain limited to startup.

An open handler does not by itself define an infinite set of routes. Open-only publication works when
the concrete notification is already known from scanned modules, handled-message assemblies, a
generated host, a generic constraint assembly, or explicit `MessageRegistration` metadata.

Registration order does not matter. A scan that finds a notification before any handler can route it
keeps that notification pending rather than discarding it, and registry creation reconsiders every
pending notification against the final registrations. Registering an open handler with
`AddNotificationHandler(typeof(AuditHandler<>))` after the scan that discovered the notifications
therefore observes them, exactly as registering it first does.

## Telemetry and failure behavior

Telemetry should wrap the combined operation: selected closed handlers followed by open generic
handlers. It should continue reporting the concrete published message type. The first exception or
cancellation should stop sequential execution, be recorded once for the publish operation, and be
re-thrown unchanged.

## Verification

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

Source generation can multiply the number of emitted route invokers by
`compatible concrete notification count × open generic handler count`; source size and startup cost
should therefore be monitored when applications contain thousands of notification types.

## Non-goals

- Open generic query or command handlers.
- Broadcasting closed notification handlers across all compatible hierarchy levels.
- Dispatch-time reflection or runtime route caching.
- Implicit scanning of every referenced assembly.
