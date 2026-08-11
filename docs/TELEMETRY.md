# OpenTelemetry design and implementation plan

## Status

Implemented on the current working branch. The API and observable semantics in this
document remain the reference for review and future refinement.

## Goals

- Add opt-in tracing and metrics for queries, commands, and notifications.
- Configure telemetry through `DispatcherOptions.Telemetry`.
- Keep telemetry disabled by default.
- Preserve the current dispatch hot path when both signals are disabled: no branch, service lookup, behavior resolution, timer read, or allocation per dispatch.
- Make telemetry the outermost operation boundary so it includes user pipeline behaviors, short-circuits, handler resolution, and sequential notification handler execution.
- Preserve scoped and transient handler and behavior semantics.
- Support both reflection-based dispatch and source-generated/Native AOT dispatch.
- Avoid a required dependency on the OpenTelemetry SDK or exporters.

## Non-goals

- Exporting telemetry or configuring an OpenTelemetry provider.
- Recording message payloads, response values, or arbitrary user data outside the standard exception event fields.
- Treating in-process notifications as broker messaging or RPC operations.
- Adding a public notification pipeline solely to host telemetry.
- Adding logs in the first version.

## Public API

Extend the existing options type with a nested configuration object:

```csharp
public sealed class DispatcherOptions
{
    public ServiceLifetime ServiceLifetime { get; set; } = ServiceLifetime.Scoped;

    public DispatcherTelemetryOptions Telemetry { get; }
}

public sealed class DispatcherTelemetryOptions
{
    public bool EnableMetrics { get; set; }

    public bool EnableTracing { get; set; }

    public string MeterName { get; set; } = DefaultInstrumentationName;

    public string ActivitySourceName { get; set; } = DefaultInstrumentationName;
}
```

Both options types belong to the `Dispatcher` namespace and the `Send0xx.Dispatcher`
package so reflection-based and source-generated registration share the same API without
the options being owned by either DI implementation package.

Example:

```csharp
builder.Services.AddDispatcher(options =>
{
    options.Telemetry.EnableMetrics = true;
    options.Telemetry.EnableTracing = true;
    options.Telemetry.MeterName = "Contoso.Orders.Dispatcher";
    options.Telemetry.ActivitySourceName = "Contoso.Orders.Dispatcher";
});
```

The booleans default to `false`. Names must be non-null and non-whitespace. Configuration is copied into registration-owned values rather than retaining the mutable options object.

`DispatcherOptions` is also used by handler and behavior registration. Those registration methods should continue to read only `ServiceLifetime`; telemetry configuration has meaning only for the reflection-based or generated `AddDispatcher` method.

`Telemetry` is created lazily. This avoids allocating telemetry options for handler or behavior registration methods that construct `DispatcherOptions` but never read telemetry. This does not affect dispatch performance.

## OpenTelemetry integration model

Use the .NET diagnostics APIs from the shared framework:

- `System.Diagnostics.ActivitySource` for traces.
- `System.Diagnostics.Metrics.Meter` for metrics.
- `Stopwatch.GetTimestamp()` for elapsed time when a metric listener is active.

Do not reference `OpenTelemetry`, `OpenTelemetry.Api`, an exporter, or an SDK package. Applications opt into collection by registering the configured source and meter names with their OpenTelemetry providers. This follows the standard .NET manual-instrumentation model documented by OpenTelemetry: [custom traces and metrics for .NET](https://opentelemetry.io/docs/zero-code/dotnet/custom/).

`ActivitySource` and `Meter` should be singleton, disposed with the root service provider, and created only for enabled signals. Use the Dispatcher package version as their instrumentation version.

## Recommended execution boundary

Use an internal, conditional dispatch instrumentation wrapper rather than registering telemetry as a normal `IPipelineBehavior<,>`.

For a routed message, the conceptual flow is:

```text
telemetry
  -> user pipeline behavior 1
    -> user pipeline behavior 2
      -> handler
```

For a notification:

```text
telemetry
  -> notification handler 1
  -> notification handler 2
  -> ...
```

The telemetry wrapper is internal infrastructure. It is selected once while registrations/routes are prepared and is not resolved from the request scope on each dispatch.

When both signals are disabled, registration must select today's uninstrumented wrappers and generated methods. No nullable telemetry field or `if (telemetry != null)` check should be added to the existing hot path.

When either signal is enabled, registration selects instrumented wrappers that surround the complete operation. The enabled implementation should retain the synchronous `ValueTask` fast path and use an async helper only when the inner operation is incomplete.

### Why not only a pipeline behavior?

A request behavior is attractive because the first registered behavior is already outermost. It nevertheless has four problems here:

1. `IPipelineBehavior<,>` only applies to `IRequest`; notifications have no pipeline.
2. A user can affect ordering through registration order unless telemetry is treated specially.
3. Resolving telemetry as a behavior on every dispatch adds behavior collection and pipeline construction costs whenever telemetry is enabled.
4. Reflection and source-generated registration would need different mechanisms to force the built-in behavior ahead of all direct Microsoft DI registrations.

A hybrid design—request behavior plus a separate notification wrapper—would work, but it duplicates completion/error logic and gives signals subtly different boundaries. A single internal dispatch-level concept is easier to specify and test.

## Runtime implementation shape

The reflection/runtime path decorates prepared handler wrappers only when telemetry is enabled:

- Query wrapper around `QueryHandlerWrapper<TResponse>`.
- Result-bearing command wrapper around `CommandWithResponseHandlerWrapper<TResponse>`.
- Resultless command wrapper around `CommandHandlerWrapperBase`.
- Notification wrapper around `NotificationHandlerWrapper`.

Typed handler registrations store a wrapper factory. At registry creation, each factory
creates either the existing plain wrapper or a telemetry decorator around that wrapper.
This keeps telemetry selection out of the handler wrapper API: wrappers continue to
handle dispatch only and do not expose a `WithTelemetry`-style configuration method.
Typed registration remains reflection-free; the reflection registration path constructs
the closed factory type once during startup.

The singleton registry may retain the telemetry recorder because the recorder contains only singleton `ActivitySource`, `Meter`, and instruments. It must not retain a scoped provider, handler, behavior, or executable pipeline.

The existing plain wrapper instances remain unchanged and are placed into the frozen dictionaries when telemetry is disabled. This is the key to the zero-cost disabled path and also ensures resolving the public concrete `Dispatcher` cannot bypass instrumentation for routed messages.

### Routing boundary

The wrapper design starts telemetry after a query/command route has been found and after a notification route has been found. Consequently:

- Missing query/command handlers are not recorded.
- A notification with zero handlers is not recorded.
- Handler resolution failures, behavior failures, short-circuits, cancellations, and handler failures are recorded.

The distinction is between recording actual message processing and recording every call to a dispatcher API:

- `QueryAsync(new UnknownQuery())` throws before a handler wrapper is reached. A wrapper cannot record that failed attempt.
- `PublishAsync(new UnobservedNotification())` returns immediately when no notification wrapper exists. A wrapper cannot record that successful no-op.

This matches the original pipeline-oriented proposal: telemetry describes message processing after routing succeeds. If every dispatcher API attempt must be visible, instrumentation must instead start before route lookup. That requires a dispatcher decorator or a separate instrumented dispatcher implementation. An interface-only decorator introduces another concern: resolving the public concrete `Dispatcher` directly could bypass telemetry.

The routed-message boundary is settled for the first version. Missing query/command handlers and notifications with zero handlers intentionally emit no telemetry.

## Source-generated implementation shape

Generated dispatch must implement the same observable boundary without reflection:

- Emit the current uninstrumented route functions unchanged for disabled telemetry.
- Emit instrumented route functions or an instrumented generated dispatcher variant for enabled telemetry.
- Start telemetry only after a route is found, matching the runtime path.
- Wrap the entire generated query/command pipeline and the entire sequential notification loop.
- Do not emit or resolve open generic telemetry behaviors.

The generator can emit private/internal telemetry helpers into the consuming assembly, as it already emits the dispatcher implementation and attributes. This avoids exposing generator-only implementation contracts from the runtime packages. Shared names, tags, and completion rules must be represented by tests in both implementations to prevent drift.

The generated `AddDispatcher` method must interpret `DispatcherOptions.Telemetry` exactly like the reflection registration method and remain trimming/Native AOT safe.

## Telemetry schema

Dispatcher is an in-process library, so use custom `dispatcher.*` attributes and `ActivityKind.Internal`. Do not apply OpenTelemetry RPC or messaging semantic conventions. Notification dispatch here is an in-memory fan-out operation, not a message broker publish.

### Trace

- Activity source: configurable, default `Dispatcher`.
- Activity kind: `Internal`.
- Activity name: `{operation} {message-name}`, for example `query GetOrderQuery` or `publish OrderCreated`.
- Start/end: immediately around the complete routed operation.

Initial attributes:

| Attribute | Values | Notes |
| --- | --- | --- |
| `dispatcher.operation.name` | `query`, `execute`, `publish` | Low cardinality. |
| `dispatcher.message.type` | Fully qualified CLR type name | Bounded by the application's declared message types. |
| `dispatcher.message.kind` | `query`, `command`, `notification` | Separates both command shapes from notifications and queries. |
| `error.type` | Fully qualified exception type | Present only for failures treated as errors. |

The operation and message attributes needed for sampling should be supplied when starting the activity. `ActivitySource.StartActivity` returns `null` when there is no interested listener, so tags and error details should not be constructed in that case. See the [.NET `ActivitySource` API](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource).

Do not record payloads or response values.

For a failed operation, add the exception to the activity as the standard `exception` span event. `Activity.AddException(exception)` is available on .NET 9 and later and adds `exception.type`, `exception.message`, and `exception.stacktrace`. Dispatcher targets .NET 8 and .NET 10, so use:

- `Activity.AddException(exception)` for `NET9_0_OR_GREATER`.
- `Activity.AddEvent(...)` on .NET 8 to emit the equivalent `exception` event and three attributes explicitly.

The .NET 8 fallback keeps the exported span schema consistent. It cannot invoke the `ActivityListener.ExceptionRecorder` callback because that callback is also a newer API. Exception events are emitted only when an activity was created and requested full data. Exception messages and stack traces can contain application data; enabling tracing opts into recording these standard exception details.

The implementation approach follows the useful parts of [Mediator PR #267](https://github.com/martinothamar/Mediator/pull/267), while adding the .NET 8 event fallback rather than omitting exception events on that target.

### Metrics

Start with one instrument:

- Meter name: configurable, default `Dispatcher`.

| Instrument | Type | Unit | Description |
| --- | --- | --- | --- |
| `dispatcher.operation.duration` | `Histogram<double>` | `s` | Duration of a routed Dispatcher operation. |

Use the same operation, message, kind, and conditional `error.type` attributes as tracing. A duration histogram already exports a count and sum, so a separate operation counter is not needed in the first version. OpenTelemetry's naming guidance recommends the `.duration` suffix for operation histograms and seconds as the duration unit: [metric naming](https://opentelemetry.io/docs/specs/semconv/general/naming/) and [metric units](https://opentelemetry.io/docs/specs/semconv/general/metrics/).

Check whether the histogram has an active listener before reading the timestamp or creating tags. Listener state can change after startup, so it must not be cached as a permanent boolean.

### Completion rules

- Normal completion, including a pipeline short-circuit: no `error.type`; leave activity status unset.
- Any exception, including `OperationCanceledException` and `TaskCanceledException`: add the standard exception span event, set `ActivityStatusCode.Error` and `error.type` to the concrete exception type, then rethrow the original exception unchanged.
- Cancellation can be identified from `error.type`; no separate Dispatcher status attribute is emitted.
- Telemetry must never swallow, wrap, or replace dispatch exceptions.
- Nested dispatch naturally becomes a child activity through `Activity.Current`.

The OpenTelemetry semantic-convention guidance recommends defining clear span boundaries, low-cardinality names, duration metrics, and `error.type`; it also notes that cancellation can be context-dependent: [semantic convention design guidance](https://opentelemetry.io/docs/specs/semconv/how-to-write-conventions/).

## Alternatives considered

| Design | Notifications | Disabled hot path | Outermost guarantee | Assessment |
| --- | --- | --- | --- | --- |
| Normal open generic pipeline behavior | No | Yes, if not registered | Registration-order dependent | Reject as the complete solution. |
| Request behavior plus notification special case | Yes | Yes | Requires special DI ordering | Viable, but duplicates semantics and implementation. |
| Nullable telemetry field checked in every dispatch | Yes | No | Yes | Reject because disabled dispatch pays a branch/indirection. |
| Interface-level dispatcher decorator | Yes, including zero handlers | Yes | Yes | Simple, but direct concrete `Dispatcher` resolution can bypass it. |
| Conditional prepared/generated wrappers | Yes, for routed notifications | Yes | Yes | Recommended. Preserves the concrete runtime dispatcher and current disabled path. |

## Registration and lifetime rules

- Telemetry configuration is read only by `AddDispatcher` and generated dispatcher registration.
- Both flags false means no telemetry services or instrumented wrappers are registered.
- The telemetry recorder and diagnostic sources are singleton infrastructure.
- Dispatcher lifetime remains scoped by default and may remain transient.
- Handler and behavior lifetime configuration is unchanged.
- User behaviors retain their existing order; telemetry is outside that order.
- Repeated `AddDispatcher` calls keep the current first-registration-wins behavior. `TryAdd` makes reflection registration idempotent and generated registration returns early when it finds the generated dispatcher descriptor.
- A later call with different lifetime or telemetry options does not replace the first configuration. This is consistent with current behavior and avoids changing the existing idempotence contract.
- Modules should continue to call `AddDispatcherHandlers`, not `AddDispatcher`.

## Tests

Add integration tests for both runtime and generated dispatch:

- Telemetry disabled by default and no telemetry implementation is registered/selected.
- Metrics-only and tracing-only configurations are independent.
- Custom meter and activity source names are honored.
- Query, result-bearing command, resultless command, and notification produce telemetry.
- A notification with multiple handlers produces one operation around the complete ordered sequence.
- Telemetry is outside all user behaviors and includes short-circuits.
- Successful operations omit `error.type`; exceptions and cancellations report the concrete exception type and a standard exception span event.
- Exceptions are rethrown unchanged.
- Nested dispatch produces parent/child activities.
- No payload or response data is emitted.
- Direct Microsoft DI behavior registration remains inside telemetry.
- Scoped/transient behavior resolution remains unchanged.
- Repeated registration preserves the first dispatcher and telemetry configuration.
- Runtime and generated telemetry use the same names, attributes, and completion rules.
- Generated telemetry compiles under trimming/Native AOT and performs no reflection.

Use `ActivityListener` and `MeterListener` in tests so telemetry can be asserted without adding the OpenTelemetry SDK to the test dependency graph.

## Benchmarks

Benchmark before and after implementation in Release with `MemoryDiagnoser`.

Required scenarios for reflection and generated implementations:

- Disabled query, both command shapes, one-handler notification, and multi-handler notification.
- Tracing enabled with no listener.
- Tracing enabled with a listener.
- Metrics enabled with no listener.
- Metrics enabled with a listener.
- Both enabled.
- Synchronously completing and asynchronously completing handlers.
- Warmed scope and fresh scope per dispatch.

Acceptance criteria:

- Disabled benchmark code path is structurally the same as today and preserves the current zero-allocation targets.
- No statistically meaningful disabled latency regression should be accepted without investigation.
- Enabled overhead and allocations are documented rather than assigned a target before measurement.

Measured on .NET 10.0.10 on Apple M5 Pro Arm64, a synchronously completing query was
allocation-free at 21.15 ns with telemetry disabled. Enabled telemetry without listeners
remained allocation-free at 23–25 ns. With active listeners, metrics measured 44.93 ns
and 0 B, tracing measured 119.66 ns and 608 B, and both measured 141.17 ns and 608 B.
The benchmark project contains the reproducible matrix; these values are regression
indicators rather than cross-machine guarantees.

## Implementation sequence

1. Add the options types, validation, immutable configuration snapshot, and XML documentation.
2. Implement the runtime telemetry recorder and conditional prepared-wrapper decoration.
3. Add standard exception events, including the explicit .NET 8 fallback.
4. Add runtime listener-based tests and disabled/enabled benchmarks.
5. Implement equivalent generated routes/helpers without reflection.
6. Add generator output tests, generated integration tests, and Native AOT verification.
7. Run the full Release build and .NET 8/.NET 10 tests.
8. Pack all libraries and verify that no OpenTelemetry SDK dependency was introduced.
9. Document application-side `AddSource`/`AddMeter` setup in the README and update package release notes.

## Settled decisions

- The default instrumentation name is `Dispatcher`.
- Telemetry covers only messages with found handlers. Missing request handlers and notifications with no handlers emit no telemetry.
- The first metrics version contains only `dispatcher.operation.duration`.
- Exceptions, including cancellation, use `error.type`, error activity status, and a standard exception span event. There is no Dispatcher-specific status attribute.
- Repeated `AddDispatcher` calls remain idempotent and the first configuration wins.
