using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using Dispatcher.Benchmarks.Shared;
using Dispatcher.SourceGeneration;

[assembly: GenerateDispatcher("AddGeneratedTelemetryDispatcher")]

namespace Dispatcher.Benchmarks.Telemetry;

public enum TelemetryMode
{
    Disabled,
    MetricsWithoutListener,
    TracingWithoutListener,
    MetricsAndTracingWithoutListeners,
    MetricsWithListener,
    TracingWithListener,
    MetricsAndTracingWithListeners
}

public enum OperationOutcome
{
    Successful,
    Failed
}

[DispatcherBenchmark]
public class TelemetryBenchmarks
{
    private static readonly PingQuery Query = new(41);
    private static readonly FailingQuery FailingQuery = new();

    private BenchmarkProvider _provider = null!;
    private BenchmarkHost _host = null!;
    private ActivityListener? _activityListener;
    private MeterListener? _meterListener;
    private bool _captureTags;
    private string? _activityMessageType;
    private string? _metricMessageType;

    [ParamsAllValues]
    public BenchmarkImplementation Implementation { get; set; }

    [ParamsSource(nameof(Modes))]
    public TelemetryMode Mode { get; set; }

    [ParamsAllValues]
    public OperationOutcome Outcome { get; set; }

    public static IEnumerable<TelemetryMode> Modes =>
        Environment.GetEnvironmentVariable("DISPATCHER_BENCHMARK_PROFILE") == "quick"
            ?
            [
                TelemetryMode.Disabled,
                TelemetryMode.MetricsAndTracingWithoutListeners,
                TelemetryMode.MetricsAndTracingWithListeners
            ]
            : Enum.GetValues<TelemetryMode>();

    [GlobalSetup]
    public async Task Setup()
    {
        var instrumentationName = $"Dispatcher.Benchmarks.{Implementation}.{Mode}.{Outcome}";
        var metricsEnabled = Mode is TelemetryMode.MetricsWithoutListener or
            TelemetryMode.MetricsAndTracingWithoutListeners or
            TelemetryMode.MetricsWithListener or
            TelemetryMode.MetricsAndTracingWithListeners;
        var tracingEnabled = Mode is TelemetryMode.TracingWithoutListener or
            TelemetryMode.MetricsAndTracingWithoutListeners or
            TelemetryMode.TracingWithListener or
            TelemetryMode.MetricsAndTracingWithListeners;

        if (Mode is TelemetryMode.TracingWithListener or TelemetryMode.MetricsAndTracingWithListeners)
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == instrumentationName,
                Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    if (_captureTags)
                    {
                        _activityMessageType = activity.GetTagItem("dispatcher.message.type") as string;
                    }
                }
            };
            ActivitySource.AddActivityListener(_activityListener);
        }

        if (Mode is TelemetryMode.MetricsWithListener or TelemetryMode.MetricsAndTracingWithListeners)
        {
            _meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == instrumentationName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _meterListener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
            {
                if (!_captureTags)
                {
                    return;
                }

                foreach (var tag in tags)
                {
                    if (tag.Key == "dispatcher.message.type")
                    {
                        _metricMessageType = tag.Value as string;
                    }
                }
            });
            _meterListener.Start();
        }

        _provider = BenchmarkProvider.Create(
            Implementation,
            static (services, configure) => services.AddGeneratedTelemetryDispatcher(configure),
            BasicHandlerRegistration.Add,
            options =>
            {
                options.Telemetry.EnableMetrics = metricsEnabled;
                options.Telemetry.EnableTracing = tracingEnabled;
                options.Telemetry.MeterName = instrumentationName;
                options.Telemetry.ActivitySourceName = instrumentationName;
            });
        _host = _provider.CreateHost();

        _captureTags = true;
        _ = await DispatchAndObserveFailure();
        _captureTags = false;
        var expectedType = Outcome == OperationOutcome.Successful
            ? typeof(PingQuery).FullName
            : typeof(FailingQuery).FullName;
        if (_activityListener is not null && _activityMessageType != expectedType ||
            _meterListener is not null && _metricMessageType != expectedType)
        {
            throw new InvalidOperationException("Telemetry tag validation failed.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _host.Dispose();
        _provider.Dispose();
        _activityListener?.Dispose();
        _meterListener?.Dispose();
    }

    [Benchmark]
    public ValueTask<bool> Dispatch() => DispatchAndObserveFailure();

    private async ValueTask<bool> DispatchAndObserveFailure()
    {
        try
        {
            _ = Outcome == OperationOutcome.Successful
                ? await _host.Dispatcher.QueryAsync(Query)
                : await _host.Dispatcher.QueryAsync(FailingQuery);
            return true;
        }
        catch (InvalidOperationException) when (Outcome == OperationOutcome.Failed)
        {
            return false;
        }
    }
}