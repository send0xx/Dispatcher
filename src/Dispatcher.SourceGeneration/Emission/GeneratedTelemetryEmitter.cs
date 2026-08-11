using System.Text;
using Dispatcher.SourceGeneration.Models;

namespace Dispatcher.SourceGeneration.Emission;

internal static class GeneratedTelemetryEmitter
{
    internal static void Append(
        StringBuilder source,
        string generatedTypeName,
        IReadOnlyCollection<HandlerModel> queries,
        IReadOnlyCollection<HandlerModel> responseCommands,
        IReadOnlyCollection<HandlerModel> commands,
        IReadOnlyCollection<HandlerModel> notifications)
    {
        AppendTelemetry(source, queries, responseCommands, commands, notifications);
        AppendDispatcher(source, generatedTypeName);
        AppendRoute(source);
    }

    private static void AppendTelemetry(
        StringBuilder source,
        IReadOnlyCollection<HandlerModel> queries,
        IReadOnlyCollection<HandlerModel> responseCommands,
        IReadOnlyCollection<HandlerModel> commands,
        IReadOnlyCollection<HandlerModel> notifications)
    {
        source.AppendLine();
        source.AppendLine();
        source.AppendLine("internal sealed class DispatcherTelemetry : global::System.IDisposable");
        source.AppendLine("{");
        source.AppendLine("    private static readonly string InstrumentationVersion =");
        source.AppendLine("        typeof(global::Dispatcher.IDispatcher).Assembly.GetName().Version?.ToString() ?? string.Empty;");
        source.AppendLine("    private readonly global::System.Diagnostics.ActivitySource? activitySource;");
        source.AppendLine("    private readonly global::System.Diagnostics.Metrics.Meter? meter;");
        source.AppendLine("    private readonly global::System.Diagnostics.Metrics.Histogram<double>? operationDuration;");
        AppendRouteDictionaryDeclaration(source, "QueryRoutes");
        AppendRouteDictionaryDeclaration(source, "ResponseCommandRoutes");
        AppendRouteDictionaryDeclaration(source, "CommandRoutes");
        AppendRouteDictionaryDeclaration(source, "NotificationRoutes");
        source.AppendLine();
        source.AppendLine("    internal DispatcherTelemetry(");
        source.AppendLine("        bool enableMetrics,");
        source.AppendLine("        bool enableTracing,");
        source.AppendLine("        string meterName,");
        source.AppendLine("        string activitySourceName)");
        source.AppendLine("    {");
        source.AppendLine("        if (enableMetrics)");
        source.AppendLine("        {");
        source.AppendLine("            meter = new global::System.Diagnostics.Metrics.Meter(meterName, InstrumentationVersion);");
        source.AppendLine("            operationDuration = meter.CreateHistogram<double>(");
        source.AppendLine("                \"dispatcher.operation.duration\",");
        source.AppendLine("                unit: \"s\",");
        source.AppendLine("                description: \"Duration of a routed Dispatcher operation.\");");
        source.AppendLine("        }");
        source.AppendLine("        if (enableTracing)");
        source.AppendLine("        {");
        source.AppendLine("            activitySource = new global::System.Diagnostics.ActivitySource(");
        source.AppendLine("                activitySourceName,");
        source.AppendLine("                InstrumentationVersion);");
        source.AppendLine("        }");
        AppendRouteDictionaryInitialization(source, "QueryRoutes", queries, "query", "query");
        AppendRouteDictionaryInitialization(
            source,
            "ResponseCommandRoutes",
            responseCommands,
            "execute",
            "command");
        AppendRouteDictionaryInitialization(source, "CommandRoutes", commands, "execute", "command");
        AppendRouteDictionaryInitialization(
            source,
            "NotificationRoutes",
            notifications,
            "publish",
            "notification");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    public void Dispose()");
        source.AppendLine("    {");
        source.AppendLine("        activitySource?.Dispose();");
        source.AppendLine("        meter?.Dispose();");
        source.AppendLine("    }");
        source.AppendLine("}");
    }

    private static void AppendDispatcher(StringBuilder source, string generatedTypeName)
    {
        source.AppendLine();
        source.AppendLine("internal sealed class TelemetryDispatcher(");
        source.Append("    ").Append(generatedTypeName).AppendLine(" inner,");
        source.AppendLine("    global::Dispatcher.DispatcherTelemetry telemetry) : global::Dispatcher.IDispatcher");
        source.AppendLine("{");
        source.AppendLine("    public async global::System.Threading.Tasks.ValueTask<TResponse> QueryAsync<TResponse>(");
        source.AppendLine("        global::Dispatcher.IQuery<TResponse> query,");
        source.AppendLine("        global::System.Threading.CancellationToken cancellationToken = default)");
        source.AppendLine("    {");
        source.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(query);");
        source.AppendLine("        var messageType = query.GetType();");
        source.Append("        if (!").Append(generatedTypeName).AppendLine(".HasQueryHandler(messageType, typeof(TResponse)))");
        source.AppendLine("        {");
        source.AppendLine("            return await inner.QueryAsync(query, cancellationToken).ConfigureAwait(false);");
        source.AppendLine("        }");
        AppendResponseInvocation(
            source,
            "telemetry.QueryRoutes[messageType]",
            "inner.QueryAsync(query, cancellationToken)");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    public async global::System.Threading.Tasks.ValueTask<TResponse> ExecuteAsync<TResponse>(");
        source.AppendLine("        global::Dispatcher.ICommand<TResponse> command,");
        source.AppendLine("        global::System.Threading.CancellationToken cancellationToken = default)");
        source.AppendLine("    {");
        source.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(command);");
        source.AppendLine("        var messageType = command.GetType();");
        source.Append("        if (!").Append(generatedTypeName).AppendLine(".HasResponseCommandHandler(messageType, typeof(TResponse)))");
        source.AppendLine("        {");
        source.AppendLine("            return await inner.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);");
        source.AppendLine("        }");
        AppendResponseInvocation(
            source,
            "telemetry.ResponseCommandRoutes[messageType]",
            "inner.ExecuteAsync(command, cancellationToken)");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    public async global::System.Threading.Tasks.ValueTask ExecuteAsync(");
        source.AppendLine("        global::Dispatcher.ICommand command,");
        source.AppendLine("        global::System.Threading.CancellationToken cancellationToken = default)");
        source.AppendLine("    {");
        source.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(command);");
        source.AppendLine("        var messageType = command.GetType();");
        source.Append("        if (!").Append(generatedTypeName).AppendLine(".HasCommandHandler(messageType))");
        source.AppendLine("        {");
        source.AppendLine("            await inner.ExecuteAsync(command, cancellationToken).ConfigureAwait(false);");
        source.AppendLine("            return;");
        source.AppendLine("        }");
        AppendInvocation(
            source,
            "telemetry.CommandRoutes[messageType]",
            "inner.ExecuteAsync(command, cancellationToken)");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    public async global::System.Threading.Tasks.ValueTask PublishAsync<TNotification>(");
        source.AppendLine("        TNotification notification,");
        source.AppendLine("        global::System.Threading.CancellationToken cancellationToken = default)");
        source.AppendLine("        where TNotification : global::Dispatcher.INotification");
        source.AppendLine("    {");
        source.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(notification);");
        source.AppendLine("        var messageType = notification.GetType();");
        source.Append("        if (!").Append(generatedTypeName).AppendLine(".HasNotificationHandlers(messageType))");
        source.AppendLine("        {");
        source.AppendLine("            await inner.PublishAsync(notification, cancellationToken).ConfigureAwait(false);");
        source.AppendLine("            return;");
        source.AppendLine("        }");
        AppendInvocation(
            source,
            "telemetry.NotificationRoutes[messageType]",
            "inner.PublishAsync(notification, cancellationToken)");
        source.AppendLine("    }");
        source.AppendLine("}");
    }

    private static void AppendResponseInvocation(
        StringBuilder source,
        string routeExpression,
        string invocationExpression)
    {
        source.Append("        var telemetryScope = ").Append(routeExpression).AppendLine(".Start();");
        source.AppendLine("        try");
        source.AppendLine("        {");
        source.Append("            var result = await ").Append(invocationExpression)
            .AppendLine(".ConfigureAwait(false);");
        source.AppendLine("            telemetryScope.Complete();");
        source.AppendLine("            return result;");
        source.AppendLine("        }");
        source.AppendLine("        catch (global::System.Exception exception)");
        source.AppendLine("        {");
        source.AppendLine("            telemetryScope.Fail(exception);");
        source.AppendLine("            throw;");
        source.AppendLine("        }");
    }

    private static void AppendInvocation(
        StringBuilder source,
        string routeExpression,
        string invocationExpression)
    {
        source.Append("        var telemetryScope = ").Append(routeExpression).AppendLine(".Start();");
        source.AppendLine("        try");
        source.AppendLine("        {");
        source.Append("            await ").Append(invocationExpression)
            .AppendLine(".ConfigureAwait(false);");
        source.AppendLine("            telemetryScope.Complete();");
        source.AppendLine("        }");
        source.AppendLine("        catch (global::System.Exception exception)");
        source.AppendLine("        {");
        source.AppendLine("            telemetryScope.Fail(exception);");
        source.AppendLine("            throw;");
        source.AppendLine("        }");
    }

    private static void AppendRoute(StringBuilder source)
    {
        source.AppendLine();
        source.AppendLine("internal sealed class DispatcherTelemetryRoute");
        source.AppendLine("{");
        source.AppendLine("    private readonly global::System.Diagnostics.ActivitySource? activitySource;");
        source.AppendLine("    private readonly global::System.Diagnostics.Metrics.Histogram<double>? operationDuration;");
        source.AppendLine("    private readonly string spanName;");
        source.AppendLine("    private readonly global::System.Collections.Generic.KeyValuePair<string, object?> operationTag;");
        source.AppendLine("    private readonly global::System.Collections.Generic.KeyValuePair<string, object?> messageTypeTag;");
        source.AppendLine("    private readonly global::System.Collections.Generic.KeyValuePair<string, object?> messageKindTag;");
        source.AppendLine("    private readonly global::System.Collections.Generic.KeyValuePair<string, object?>[] activityTags;");
        source.AppendLine();
        source.AppendLine("    internal DispatcherTelemetryRoute(");
        source.AppendLine("        global::System.Diagnostics.ActivitySource? activitySource,");
        source.AppendLine("        global::System.Diagnostics.Metrics.Histogram<double>? operationDuration,");
        source.AppendLine("        global::System.Type messageType,");
        source.AppendLine("        string operationName,");
        source.AppendLine("        string messageKind)");
        source.AppendLine("    {");
        source.AppendLine("        var messageName = messageType.FullName ?? messageType.Name;");
        source.AppendLine("        this.activitySource = activitySource;");
        source.AppendLine("        this.operationDuration = operationDuration;");
        source.AppendLine("        spanName = operationName + \" \" + messageType.Name;");
        source.AppendLine("        operationTag = new(\"dispatcher.operation.name\", operationName);");
        source.AppendLine("        messageTypeTag = new(\"dispatcher.message.type\", messageName);");
        source.AppendLine("        messageKindTag = new(\"dispatcher.message.kind\", messageKind);");
        source.AppendLine("        activityTags = [operationTag, messageTypeTag, messageKindTag];");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    internal global::Dispatcher.DispatcherTelemetryScope Start()");
        source.AppendLine("    {");
        source.AppendLine("        var activity = activitySource?.StartActivity(");
        source.AppendLine("            spanName,");
        source.AppendLine("            global::System.Diagnostics.ActivityKind.Internal,");
        source.AppendLine("            default(global::System.Diagnostics.ActivityContext),");
        source.AppendLine("            activityTags);");
        source.AppendLine("        var metricsEnabled = operationDuration?.Enabled == true;");
        source.AppendLine("        return new global::Dispatcher.DispatcherTelemetryScope(");
        source.AppendLine("            this,");
        source.AppendLine("            activity,");
        source.AppendLine("            metricsEnabled ? global::System.Diagnostics.Stopwatch.GetTimestamp() : 0,");
        source.AppendLine("            metricsEnabled);");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    internal void RecordSuccess(long startTimestamp) =>");
        source.AppendLine("        operationDuration!.Record(");
        source.AppendLine("            global::System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,");
        source.AppendLine("            operationTag,");
        source.AppendLine("            messageTypeTag,");
        source.AppendLine("            messageKindTag);");
        source.AppendLine();
        source.AppendLine("    internal void RecordFailure(long startTimestamp, string? errorType)");
        source.AppendLine("    {");
        source.AppendLine("        var tags = new global::System.Diagnostics.TagList");
        source.AppendLine("        {");
        source.AppendLine("            operationTag,");
        source.AppendLine("            messageTypeTag,");
        source.AppendLine("            messageKindTag,");
        source.AppendLine("            { \"error.type\", errorType }");
        source.AppendLine("        };");
        source.AppendLine("        operationDuration!.Record(");
        source.AppendLine("            global::System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,");
        source.AppendLine("            tags);");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    internal static void RecordException(");
        source.AppendLine("        global::System.Diagnostics.Activity activity,");
        source.AppendLine("        global::System.Exception exception)");
        source.AppendLine("    {");
        source.AppendLine("        if (activity.IsAllDataRequested)");
        source.AppendLine("        {");
        source.AppendLine("#if NET9_0_OR_GREATER");
        source.AppendLine("            activity.AddException(exception);");
        source.AppendLine("#else");
        source.AppendLine("            activity.AddEvent(new global::System.Diagnostics.ActivityEvent(");
        source.AppendLine("                \"exception\",");
        source.AppendLine("                tags: new global::System.Diagnostics.ActivityTagsCollection");
        source.AppendLine("                {");
        source.AppendLine("                    { \"exception.type\", exception.GetType().FullName },");
        source.AppendLine("                    { \"exception.message\", exception.Message },");
        source.AppendLine("                    { \"exception.stacktrace\", exception.ToString() }");
        source.AppendLine("                }));");
        source.AppendLine("#endif");
        source.AppendLine("        }");
        source.AppendLine("        activity.SetStatus(global::System.Diagnostics.ActivityStatusCode.Error);");
        source.AppendLine("        activity.SetTag(\"error.type\", exception.GetType().FullName);");
        source.AppendLine("    }");
        source.AppendLine("}");
        source.AppendLine();
        source.AppendLine("internal readonly struct DispatcherTelemetryScope(");
        source.AppendLine("    global::Dispatcher.DispatcherTelemetryRoute route,");
        source.AppendLine("    global::System.Diagnostics.Activity? activity,");
        source.AppendLine("    long startTimestamp,");
        source.AppendLine("    bool metricsEnabled)");
        source.AppendLine("{");
        source.AppendLine("    internal void Complete()");
        source.AppendLine("    {");
        source.AppendLine("        try");
        source.AppendLine("        {");
        source.AppendLine("            if (metricsEnabled)");
        source.AppendLine("            {");
        source.AppendLine("                route.RecordSuccess(startTimestamp);");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine("        finally");
        source.AppendLine("        {");
        source.AppendLine("            activity?.Dispose();");
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    internal void Fail(global::System.Exception exception)");
        source.AppendLine("    {");
        source.AppendLine("        try");
        source.AppendLine("        {");
        source.AppendLine("            if (activity is not null)");
        source.AppendLine("            {");
        source.AppendLine("                global::Dispatcher.DispatcherTelemetryRoute.RecordException(activity, exception);");
        source.AppendLine("            }");
        source.AppendLine("            if (metricsEnabled)");
        source.AppendLine("            {");
        source.AppendLine("                route.RecordFailure(startTimestamp, exception.GetType().FullName);");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine("        finally");
        source.AppendLine("        {");
        source.AppendLine("            activity?.Dispose();");
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");
    }

    private static void AppendRouteDictionaryDeclaration(StringBuilder source, string name)
    {
        source.AppendLine();
        source.Append("    internal global::System.Collections.Frozen.FrozenDictionary<global::System.Type, global::Dispatcher.DispatcherTelemetryRoute> ")
            .Append(name).AppendLine(" { get; }");
    }

    private static void AppendRouteDictionaryInitialization(
        StringBuilder source,
        string name,
        IEnumerable<HandlerModel> handlers,
        string operationName,
        string messageKind)
    {
        source.Append("        ").Append(name).AppendLine(" =");
        source.AppendLine("            new global::System.Collections.Generic.Dictionary<global::System.Type, global::Dispatcher.DispatcherTelemetryRoute>");
        source.AppendLine("            {");
        foreach (var handler in handlers)
        {
            var messageType = handler.MessageType.ToDisplayString(SymbolDisplayFormats.FullyQualified);
            source.Append("                [typeof(").Append(messageType)
                .Append(")] = new global::Dispatcher.DispatcherTelemetryRoute(activitySource, operationDuration, typeof(")
                .Append(messageType).Append("), \"").Append(operationName).Append("\", \"")
                .Append(messageKind).AppendLine("\"),");
        }
        source.AppendLine("            }.ToFrozenDictionary();");
    }
}