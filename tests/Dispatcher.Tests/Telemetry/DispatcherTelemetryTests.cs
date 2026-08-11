using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dispatcher.DependencyInjection;
using Dispatcher.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.Tests.Telemetry;

public sealed class DispatcherTelemetryTests
{
    [Fact]
    public void Telemetry_is_disabled_and_uses_dispatcher_instrumentation_names_by_default()
    {
        var telemetry = new DispatcherOptions().Telemetry;

        Assert.Same(typeof(DispatcherRegistry).Assembly, typeof(DispatcherOptions).Assembly);
        Assert.Same(typeof(DispatcherRegistry).Assembly, typeof(DispatcherTelemetryOptions).Assembly);
        Assert.False(telemetry.EnableMetrics);
        Assert.False(telemetry.EnableTracing);
        Assert.Equal("Dispatcher", telemetry.MeterName);
        Assert.Equal("Dispatcher", telemetry.ActivitySourceName);
    }

    [Fact]
    public async Task Metrics_can_be_enabled_without_tracing()
    {
        var instrumentationName = "Dispatcher.Tests." + Guid.NewGuid();
        using var capture = new TelemetryCapture(instrumentationName, instrumentationName);
        await using var provider = CreateProvider(options =>
        {
            options.Telemetry.EnableMetrics = true;
            options.Telemetry.MeterName = instrumentationName;
            options.Telemetry.ActivitySourceName = instrumentationName;
        });
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Ada"));

        Assert.Empty(capture.Activities);
        Assert.Single(capture.Measurements);
    }

    [Fact]
    public async Task Emits_one_activity_and_duration_for_every_routed_message_shape()
    {
        var instrumentationName = "Dispatcher.Tests." + Guid.NewGuid();
        using var capture = new TelemetryCapture(instrumentationName, instrumentationName);
        await using var provider = CreateProvider(options =>
        {
            options.Telemetry.EnableMetrics = true;
            options.Telemetry.EnableTracing = true;
            options.Telemetry.MeterName = instrumentationName;
            options.Telemetry.ActivitySourceName = instrumentationName;
        });
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.QueryAsync(new GreetingQuery("Ada"));
        await dispatcher.ExecuteAsync(new SumCommand(2, 3));
        await dispatcher.ExecuteAsync(new RecordCommand("recorded"));
        await dispatcher.PublishAsync(new SomethingHappened());

        Assert.Equal(
            [
                "query GreetingQuery",
                "execute SumCommand",
                "execute RecordCommand",
                "publish SomethingHappened"
            ],
            capture.Activities.Select(activity => activity.DisplayName));
        Assert.Equal(4, capture.Measurements.Count);
        Assert.All(capture.Activities, activity =>
        {
            Assert.Equal(ActivityKind.Internal, activity.Kind);
            Assert.Equal(ActivityStatusCode.Unset, activity.Status);
            Assert.Null(activity.GetTagItem("error.type"));
        });
        Assert.All(capture.Measurements, measurement =>
        {
            Assert.Equal("dispatcher.operation.duration", measurement.InstrumentName);
            Assert.True(measurement.Value >= 0);
            Assert.DoesNotContain(measurement.Tags, tag => tag.Key == "error.type");
        });
        Assert.Single(capture.Activities, activity =>
            Equals(activity.GetTagItem("dispatcher.message.kind"), "notification"));
    }

    [Fact]
    public async Task Telemetry_is_outside_user_pipeline_behaviors()
    {
        var instrumentationName = "Dispatcher.Tests." + Guid.NewGuid();
        using var capture = new TelemetryCapture(instrumentationName, meterName: null);
        var services = CreateServices(options =>
        {
            options.Telemetry.EnableTracing = true;
            options.Telemetry.ActivitySourceName = instrumentationName;
        });
        services.AddPipelineBehavior<TelemetryObservingGreetingBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Ada"));

        Assert.Equal(
            instrumentationName,
            scope.ServiceProvider.GetRequiredService<TestState>().Recorded);
        Assert.Single(capture.Activities);
    }

    [Fact]
    public async Task Missing_requests_and_notifications_without_handlers_emit_no_telemetry()
    {
        var instrumentationName = "Dispatcher.Tests." + Guid.NewGuid();
        using var capture = new TelemetryCapture(instrumentationName, instrumentationName);
        await using var provider = CreateProvider(options =>
        {
            options.Telemetry.EnableMetrics = true;
            options.Telemetry.EnableTracing = true;
            options.Telemetry.MeterName = instrumentationName;
            options.Telemetry.ActivitySourceName = instrumentationName;
        });
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.PublishAsync(new UnhandledNotification());
        await Assert.ThrowsAsync<HandlerNotFoundException>(() =>
            dispatcher.QueryAsync(new MissingQuery()).AsTask());

        Assert.Empty(capture.Activities);
        Assert.Empty(capture.Measurements);
    }

    [Fact]
    public async Task Exceptions_set_error_type_and_add_standard_span_event()
    {
        var instrumentationName = "Dispatcher.Tests." + Guid.NewGuid();
        using var capture = new TelemetryCapture(instrumentationName, instrumentationName);
        await using var provider = CreateProvider(options =>
        {
            options.Telemetry.EnableMetrics = true;
            options.Telemetry.EnableTracing = true;
            options.Telemetry.MeterName = instrumentationName;
            options.Telemetry.ActivitySourceName = instrumentationName;
        });
        await using var scope = provider.CreateAsyncScope();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
                .QueryAsync(new FaultingQuery()).AsTask());

        var activity = Assert.Single(capture.Activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(
            typeof(InvalidOperationException).FullName,
            activity.GetTagItem("error.type"));
        var exceptionEvent = Assert.Single(activity.Events);
        Assert.Equal("exception", exceptionEvent.Name);
        Assert.Contains(exceptionEvent.Tags, tag =>
            tag.Key == "exception.type" &&
            Equals(tag.Value, typeof(InvalidOperationException).FullName));
        Assert.Contains(exceptionEvent.Tags, tag =>
            tag.Key == "exception.message" &&
            Equals(tag.Value, "telemetry failure"));
        Assert.Contains(exceptionEvent.Tags, tag => tag.Key == "exception.stacktrace");

        var measurement = Assert.Single(capture.Measurements);
        Assert.Contains(measurement.Tags, tag =>
            tag.Key == "error.type" &&
            Equals(tag.Value, typeof(InvalidOperationException).FullName));
    }

    [Fact]
    public async Task Cancellation_is_recorded_as_an_exception()
    {
        var instrumentationName = "Dispatcher.Tests." + Guid.NewGuid();
        using var capture = new TelemetryCapture(instrumentationName, meterName: null);
        await using var provider = CreateProvider(options =>
        {
            options.Telemetry.EnableTracing = true;
            options.Telemetry.ActivitySourceName = instrumentationName;
        });
        await using var scope = provider.CreateAsyncScope();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
                .ExecuteAsync(new CancellingCommand()).AsTask());

        var activity = Assert.Single(capture.Activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal(
            typeof(OperationCanceledException).FullName,
            activity.GetTagItem("error.type"));
        Assert.Equal("exception", Assert.Single(activity.Events).Name);
    }

    [Fact]
    public async Task First_dispatcher_telemetry_configuration_wins()
    {
        var firstName = "Dispatcher.Tests.First." + Guid.NewGuid();
        var secondName = "Dispatcher.Tests.Second." + Guid.NewGuid();
        using var firstCapture = new TelemetryCapture(firstName, meterName: null);
        using var secondCapture = new TelemetryCapture(secondName, meterName: null);
        var services = new ServiceCollection();
        services.AddDispatcher(options =>
        {
            options.Telemetry.EnableTracing = true;
            options.Telemetry.ActivitySourceName = firstName;
        });
        services.AddDispatcher(options =>
        {
            options.Telemetry.EnableTracing = true;
            options.Telemetry.ActivitySourceName = secondName;
        });
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        services.AddScoped<TestState>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Ada"));

        Assert.Single(firstCapture.Activities);
        Assert.Empty(secondCapture.Activities);
    }

    private static ServiceProvider CreateProvider(Action<DispatcherOptions> configure) =>
        TestServices.BuildProvider(CreateServices(configure));

    private static ServiceCollection CreateServices(Action<DispatcherOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddDispatcher(configure);
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        services.AddScoped<TestState>();
        return services;
    }
}

internal sealed class TelemetryObservingGreetingBehavior(TestState state)
    : IPipelineBehavior<GreetingQuery, string>
{
    public ValueTask<string> HandleAsync(
        GreetingQuery request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        state.Recorded = Activity.Current?.Source.Name;
        return next(cancellationToken);
    }
}

internal sealed class TelemetryCapture : IDisposable
{
    private readonly ActivityListener? _activityListener;
    private readonly MeterListener? _meterListener;

    internal TelemetryCapture(string? activitySourceName, string? meterName)
    {
        if (activitySourceName is not null)
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == activitySourceName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => Activities.Enqueue(activity)
            };
            ActivitySource.AddActivityListener(_activityListener);
        }

        if (meterName is not null)
        {
            _meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == meterName &&
                        instrument.Name == "dispatcher.operation.duration")
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            {
                var copiedTags = new KeyValuePair<string, object?>[tags.Length];
                tags.CopyTo(copiedTags);
                Measurements.Enqueue(new MetricMeasurement(instrument.Name, value, copiedTags));
            });
            _meterListener.Start();
        }
    }

    internal ConcurrentQueue<Activity> Activities { get; } = new();
    internal ConcurrentQueue<MetricMeasurement> Measurements { get; } = new();

    public void Dispose()
    {
        _activityListener?.Dispose();
        _meterListener?.Dispose();
    }
}

internal sealed record MetricMeasurement(
    string InstrumentName,
    double Value,
    IReadOnlyList<KeyValuePair<string, object?>> Tags);
