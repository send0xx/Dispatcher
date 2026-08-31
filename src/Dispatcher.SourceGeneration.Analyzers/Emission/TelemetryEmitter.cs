using Dispatcher.SourceGeneration.Model;
using Dispatcher.SourceGeneration.Support;

namespace Dispatcher.SourceGeneration.Emission;

internal static class TelemetryEmitter
{
    internal static void Write(CodeWriter code, DispatcherSourceModel model)
    {
        WriteTelemetryResources(code, model);
        WriteDecoratedDispatcher(code);
        WriteTelemetryRoute(code);
    }

    private static void WriteTelemetryResources(CodeWriter code, DispatcherSourceModel model)
    {
        code.Lines("""

            internal sealed class DispatcherTelemetry : global::System.IDisposable
            {
                private static readonly string InstrumentationVersion =
                    typeof(global::Dispatcher.IDispatcher).Assembly.GetName().Version?.ToString() ?? string.Empty;
                private readonly global::System.Diagnostics.ActivitySource? activitySource;
                private readonly global::System.Diagnostics.Metrics.Meter? meter;
                private readonly global::System.Diagnostics.Metrics.Histogram<double>? operationDuration;

                internal global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Dispatcher.SourceGeneration.DispatcherTelemetryRoute> QueryRoutes { get; }
                internal global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Dispatcher.SourceGeneration.DispatcherTelemetryRoute> ResponseCommandRoutes { get; }
                internal global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Dispatcher.SourceGeneration.DispatcherTelemetryRoute> CommandRoutes { get; }
                internal global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Dispatcher.SourceGeneration.DispatcherTelemetryRoute> NotificationRoutes { get; }

                internal DispatcherTelemetry(
                    bool enableMetrics,
                    bool enableTracing,
                    string meterName,
                    string activitySourceName)
                {
                    if (enableMetrics)
                    {
                        meter = new global::System.Diagnostics.Metrics.Meter(meterName, InstrumentationVersion);
                        operationDuration = meter.CreateHistogram<double>(
                            "dispatcher.operation.duration",
                            unit: "s",
                            description: "Duration of a routed Dispatcher operation.");
                    }
                    if (enableTracing)
                    {
                        activitySource = new global::System.Diagnostics.ActivitySource(
                            activitySourceName,
                            InstrumentationVersion);
                    }
            """);
        WriteRouteTable(code, "QueryRoutes", model.Queries, "query", "query");
        WriteRouteTable(code, "ResponseCommandRoutes", model.ResponseCommands, "execute", "command");
        WriteRouteTable(code, "CommandRoutes", model.Commands, "execute", "command");
        WriteRouteTable(code, "NotificationRoutes", model.Notifications, "publish", "notification");
        code.Lines("""
                }

                public void Dispose()
                {
                    activitySource?.Dispose();
                    meter?.Dispose();
                }
            }
            """);
    }

    private static void WriteRouteTable(
        CodeWriter code,
        string property,
        IEnumerable<DispatchRoute> routes,
        string operation,
        string messageKind)
    {
        code.Line($"        {property} =");
        code.Lines("""
                        new global::System.Collections.Generic.Dictionary<global::System.Type, global::Dispatcher.SourceGeneration.DispatcherTelemetryRoute>
                        {
            """);
        foreach (var route in routes)
        {
            var message = CSharpNames.Type(route.MessageType);
            code.Line($"                [typeof({message})] = new global::Dispatcher.SourceGeneration.DispatcherTelemetryRoute(activitySource, operationDuration, typeof({message}), \"{operation}\", \"{messageKind}\"),");
        }

        code.Line("            }.ToFrozenDictionary();");
    }

    private static void WriteDecoratedDispatcher(CodeWriter code)
    {
        code.Lines("""

            internal sealed class TelemetryDispatcher(
                global::Dispatcher.SourceGeneration.Dispatcher inner,
                global::Dispatcher.SourceGeneration.DispatcherTelemetry telemetry) : global::Dispatcher.IDispatcher
            {
                public async global::System.Threading.Tasks.ValueTask<TResponse> QueryAsync<TResponse>(
                    global::Dispatcher.IQuery<TResponse> query,
                    global::System.Threading.CancellationToken cancellationToken = default)
                {
                    global::System.ArgumentNullException.ThrowIfNull(query);
                    var messageType = query.GetType();
                    if (!global::Dispatcher.SourceGeneration.Dispatcher.HasQueryHandler(messageType, typeof(TResponse)))
                    {
                        return await inner.QueryAsync(query, cancellationToken).ConfigureAwait(false);
                    }
                    var telemetryScope = telemetry.QueryRoutes[messageType].Start();
                    try
                    {
                        var result = await inner.QueryAsync(query, cancellationToken).ConfigureAwait(false);
                        telemetryScope.Complete();
                        return result;
                    }
                    catch (global::System.Exception exception)
                    {
                        telemetryScope.Fail(exception);
                        throw;
                    }
                }

                public async global::System.Threading.Tasks.ValueTask<TResponse> ExecuteAsync<TResponse>(
                    global::Dispatcher.ICommand<TResponse> command,
                    global::System.Threading.CancellationToken cancellationToken = default)
                {
                    global::System.ArgumentNullException.ThrowIfNull(command);
                    var messageType = command.GetType();
                    if (!global::Dispatcher.SourceGeneration.Dispatcher.HasResponseCommandHandler(messageType, typeof(TResponse)))
                    {
                        return await inner.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                    }
                    var telemetryScope = telemetry.ResponseCommandRoutes[messageType].Start();
                    try
                    {
                        var result = await inner.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                        telemetryScope.Complete();
                        return result;
                    }
                    catch (global::System.Exception exception)
                    {
                        telemetryScope.Fail(exception);
                        throw;
                    }
                }

                public async global::System.Threading.Tasks.ValueTask ExecuteAsync(
                    global::Dispatcher.ICommand command,
                    global::System.Threading.CancellationToken cancellationToken = default)
                {
                    global::System.ArgumentNullException.ThrowIfNull(command);
                    var messageType = command.GetType();
                    if (!global::Dispatcher.SourceGeneration.Dispatcher.HasCommandHandler(messageType))
                    {
                        await inner.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                    var telemetryScope = telemetry.CommandRoutes[messageType].Start();
                    try
                    {
                        await inner.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
                        telemetryScope.Complete();
                    }
                    catch (global::System.Exception exception)
                    {
                        telemetryScope.Fail(exception);
                        throw;
                    }
                }

                public async global::System.Threading.Tasks.ValueTask PublishAsync<TNotification>(
                    TNotification notification,
                    global::System.Threading.CancellationToken cancellationToken = default)
                    where TNotification : global::Dispatcher.INotification
                {
                    global::System.ArgumentNullException.ThrowIfNull(notification);
                    var messageType = notification.GetType();
                    if (!global::Dispatcher.SourceGeneration.Dispatcher.HasNotificationHandlers(messageType))
                    {
                        await inner.PublishAsync(notification, cancellationToken).ConfigureAwait(false);
                        return;
                    }
                    var telemetryScope = telemetry.NotificationRoutes[messageType].Start();
                    try
                    {
                        await inner.PublishAsync(notification, cancellationToken).ConfigureAwait(false);
                        telemetryScope.Complete();
                    }
                    catch (global::System.Exception exception)
                    {
                        telemetryScope.Fail(exception);
                        throw;
                    }
                }
            }
            """);
    }

    private static void WriteTelemetryRoute(CodeWriter code)
    {
        code.Lines("""

            internal sealed class DispatcherTelemetryRoute
            {
                private readonly global::System.Diagnostics.ActivitySource? activitySource;
                private readonly global::System.Diagnostics.Metrics.Histogram<double>? operationDuration;
                private readonly string spanName;
                private readonly global::System.Collections.Generic.KeyValuePair<string, object?> operationTag;
                private readonly global::System.Collections.Generic.KeyValuePair<string, object?> messageTypeTag;
                private readonly global::System.Collections.Generic.KeyValuePair<string, object?> messageKindTag;
                private readonly global::System.Collections.Generic.KeyValuePair<string, object?>[] activityTags;

                internal DispatcherTelemetryRoute(
                    global::System.Diagnostics.ActivitySource? activitySource,
                    global::System.Diagnostics.Metrics.Histogram<double>? operationDuration,
                    global::System.Type messageType,
                    string operationName,
                    string messageKind)
                {
                    var messageName = messageType.FullName ?? messageType.Name;
                    this.activitySource = activitySource;
                    this.operationDuration = operationDuration;
                    spanName = operationName + " " + messageType.Name;
                    operationTag = new("dispatcher.operation.name", operationName);
                    messageTypeTag = new("dispatcher.message.type", messageName);
                    messageKindTag = new("dispatcher.message.kind", messageKind);
                    activityTags = [operationTag, messageTypeTag, messageKindTag];
                }

                internal global::Dispatcher.SourceGeneration.DispatcherTelemetryScope Start()
                {
                    var activity = activitySource?.StartActivity(
                        spanName,
                        global::System.Diagnostics.ActivityKind.Internal,
                        default(global::System.Diagnostics.ActivityContext),
                        activityTags);
                    var metricsEnabled = operationDuration?.Enabled == true;
                    return new global::Dispatcher.SourceGeneration.DispatcherTelemetryScope(
                        this,
                        activity,
                        metricsEnabled ? global::System.Diagnostics.Stopwatch.GetTimestamp() : 0,
                        metricsEnabled);
                }

                internal void RecordSuccess(long startTimestamp) =>
                    operationDuration!.Record(
                        global::System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
                        operationTag,
                        messageTypeTag,
                        messageKindTag);

                internal void RecordFailure(long startTimestamp, string? errorType)
                {
                    var tags = new global::System.Diagnostics.TagList
                    {
                        operationTag,
                        messageTypeTag,
                        messageKindTag,
                        { "error.type", errorType }
                    };
                    operationDuration!.Record(
                        global::System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
                        tags);
                }

                internal static void RecordException(
                    global::System.Diagnostics.Activity activity,
                    global::System.Exception exception)
                {
                    if (activity.IsAllDataRequested)
                    {
            #if NET9_0_OR_GREATER
                        activity.AddException(exception);
            #else
                        activity.AddEvent(new global::System.Diagnostics.ActivityEvent(
                            "exception",
                            tags: new global::System.Diagnostics.ActivityTagsCollection
                            {
                                { "exception.type", exception.GetType().FullName },
                                { "exception.message", exception.Message },
                                { "exception.stacktrace", exception.ToString() }
                            }));
            #endif
                    }
                    activity.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error);
                    activity.SetTag("error.type", exception.GetType().FullName);
                }
            }

            internal readonly struct DispatcherTelemetryScope(
                global::Dispatcher.SourceGeneration.DispatcherTelemetryRoute route,
                global::System.Diagnostics.Activity? activity,
                long startTimestamp,
                bool metricsEnabled)
            {
                internal void Complete()
                {
                    try
                    {
                        if (metricsEnabled)
                        {
                            route.RecordSuccess(startTimestamp);
                        }
                    }
                    finally
                    {
                        activity?.Dispose();
                    }
                }

                internal void Fail(global::System.Exception exception)
                {
                    try
                    {
                        if (activity is not null)
                        {
                            global::Dispatcher.SourceGeneration.DispatcherTelemetryRoute.RecordException(activity, exception);
                        }
                        if (metricsEnabled)
                        {
                            route.RecordFailure(startTimestamp, exception.GetType().FullName);
                        }
                    }
                    finally
                    {
                        activity?.Dispose();
                    }
                }
            }
            """);
    }
}