using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Dispatcher;

/// <summary>
/// Provides tracing and metrics instrumentation used by a Dispatcher registry.
/// </summary>
/// <remarks>
/// Applications normally configure this service through <see cref="DispatcherOptions.Telemetry"/>.
/// </remarks>
public sealed class DispatcherTelemetry : IDisposable
{
    private static readonly string InstrumentationVersion =
        typeof(IDispatcher).Assembly.GetName().Version?.ToString() ?? string.Empty;

    private readonly ActivitySource? _activitySource;
    private readonly Meter? _meter;
    private readonly Histogram<double>? _operationDuration;

    /// <summary>
    /// Initializes Dispatcher telemetry from the specified configuration.
    /// </summary>
    /// <param name="options">The telemetry configuration.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public DispatcherTelemetry(DispatcherTelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.EnableMetrics)
        {
            _meter = new Meter(options.MeterName, InstrumentationVersion);
            _operationDuration = _meter.CreateHistogram<double>(
                "dispatcher.operation.duration",
                unit: "s",
                description: "Duration of a routed Dispatcher operation.");
        }

        if (options.EnableTracing)
        {
            _activitySource = new ActivitySource(options.ActivitySourceName, InstrumentationVersion);
        }
    }

    internal DispatcherTelemetryRoute CreateRoute(
        Type messageType,
        string operationName,
        string messageKind) =>
        new(
            _activitySource,
            _operationDuration,
            messageType,
            operationName,
            messageKind);

    /// <summary>
    /// Releases the activity source and meter owned by this instance.
    /// </summary>
    public void Dispose()
    {
        _activitySource?.Dispose();
        _meter?.Dispose();
    }
}

internal sealed class DispatcherTelemetryRoute
{
    private readonly ActivitySource? _activitySource;
    private readonly Histogram<double>? _operationDuration;
    private readonly string _spanName;
    private readonly KeyValuePair<string, object?> _operationTag;
    private readonly KeyValuePair<string, object?> _messageTypeTag;
    private readonly KeyValuePair<string, object?> _messageKindTag;
    private readonly KeyValuePair<string, object?>[] _activityTags;

    internal DispatcherTelemetryRoute(
        ActivitySource? activitySource,
        Histogram<double>? operationDuration,
        Type messageType,
        string operationName,
        string messageKind)
    {
        var messageName = messageType.FullName ?? messageType.Name;
        _activitySource = activitySource;
        _operationDuration = operationDuration;
        _spanName = operationName + " " + messageType.Name;
        _operationTag = new("dispatcher.operation.name", operationName);
        _messageTypeTag = new("dispatcher.message.type", messageName);
        _messageKindTag = new("dispatcher.message.kind", messageKind);
        _activityTags = [_operationTag, _messageTypeTag, _messageKindTag];
    }

    internal DispatcherTelemetryScope Start()
    {
        var activity = _activitySource?.StartActivity(
            _spanName,
            ActivityKind.Internal,
            default(ActivityContext),
            _activityTags);
        var metricsEnabled = _operationDuration?.Enabled == true;

        return new DispatcherTelemetryScope(
            this,
            activity,
            metricsEnabled ? Stopwatch.GetTimestamp() : 0,
            metricsEnabled);
    }

    internal void RecordSuccess(long startTimestamp)
    {
        _operationDuration!.Record(
            Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
            _operationTag,
            _messageTypeTag,
            _messageKindTag);
    }

    internal void RecordFailure(long startTimestamp, string? errorType)
    {
        var tags = new TagList
        {
            _operationTag,
            _messageTypeTag,
            _messageKindTag,
            { "error.type", errorType }
        };
        _operationDuration!.Record(
            Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
            tags);
    }

    internal static void RecordException(Activity activity, Exception exception)
    {
        if (activity.IsAllDataRequested)
        {
#if NET9_0_OR_GREATER
            activity.AddException(exception);
#else
            activity.AddEvent(new ActivityEvent(
                "exception",
                tags: new ActivityTagsCollection
                {
                    { "exception.type", exception.GetType().FullName },
                    { "exception.message", exception.Message },
                    { "exception.stacktrace", exception.ToString() }
                }));
#endif
        }

        activity.SetStatus(ActivityStatusCode.Error);
        activity.SetTag("error.type", exception.GetType().FullName);
    }
}

internal readonly struct DispatcherTelemetryScope(
    DispatcherTelemetryRoute route,
    Activity? activity,
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

    internal void Fail(Exception exception)
    {
        try
        {
            if (activity is not null)
            {
                DispatcherTelemetryRoute.RecordException(activity, exception);
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
