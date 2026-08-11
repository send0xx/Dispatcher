using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using Dispatcher.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks;

public enum TelemetryBenchmarkMode
{
    Disabled,
    MetricsWithoutListener,
    TracingWithoutListener,
    BothWithoutListeners,
    MetricsWithListener,
    TracingWithListener,
    BothWithListeners
}

[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "RatioSD")]
public class TelemetryBenchmarks
{
    private static readonly PingQuery QueryMessage = new(41);

    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;
    private IDispatcher _dispatcher = null!;
    private ActivityListener? _activityListener;
    private MeterListener? _meterListener;

    [ParamsAllValues]
    public TelemetryBenchmarkMode Mode { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var instrumentationName = "Dispatcher.Benchmarks." + Mode;
        var enableMetrics = Mode is
            TelemetryBenchmarkMode.MetricsWithoutListener or
            TelemetryBenchmarkMode.BothWithoutListeners or
            TelemetryBenchmarkMode.MetricsWithListener or
            TelemetryBenchmarkMode.BothWithListeners;
        var enableTracing = Mode is
            TelemetryBenchmarkMode.TracingWithoutListener or
            TelemetryBenchmarkMode.BothWithoutListeners or
            TelemetryBenchmarkMode.TracingWithListener or
            TelemetryBenchmarkMode.BothWithListeners;

        if (Mode is TelemetryBenchmarkMode.TracingWithListener or
            TelemetryBenchmarkMode.BothWithListeners)
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == instrumentationName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded
            };
            ActivitySource.AddActivityListener(_activityListener);
        }

        if (Mode is TelemetryBenchmarkMode.MetricsWithListener or
            TelemetryBenchmarkMode.BothWithListeners)
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
            _meterListener.SetMeasurementEventCallback<double>(static (_, _, _, _) => { });
            _meterListener.Start();
        }

        var services = new ServiceCollection();
        services
            .AddDispatcher(options =>
            {
                options.Telemetry.EnableMetrics = enableMetrics;
                options.Telemetry.EnableTracing = enableTracing;
                options.Telemetry.MeterName = instrumentationName;
                options.Telemetry.ActivitySourceName = instrumentationName;
            })
            .AddDispatcherHandlers<DispatchBenchmarks>();

        _provider = services.BuildServiceProvider();
        _scope = _provider.CreateScope();
        _dispatcher = _scope.ServiceProvider.GetRequiredService<IDispatcher>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _scope.Dispose();
        _provider.Dispose();
        _activityListener?.Dispose();
        _meterListener?.Dispose();
    }

    [Benchmark]
    public ValueTask<int> Query() =>
        _dispatcher.QueryAsync(QueryMessage);
}
