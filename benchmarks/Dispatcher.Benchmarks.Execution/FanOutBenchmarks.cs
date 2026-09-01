using BenchmarkDotNet.Attributes;
using Dispatcher.Benchmarks.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks.Execution;

[DispatcherBenchmark]
public class NotificationFanOutBenchmarks
{
    private static readonly FanOutNotification Notification = new();
    private BenchmarkProvider _provider = null!;
    private BenchmarkHost _host = null!;

    [ParamsAllValues]
    public BenchmarkImplementation Implementation { get; set; }

    [Params(0, 1, 5, 20, 50)]
    public int HandlerCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _provider = ExecutionHostFactory.Create(
            Implementation,
            services => FanOutRegistration.Add(services, HandlerCount));
        _host = _provider.CreateHost();

        var state = _host.Services.GetRequiredService<FanOutState>();
        state.ValidateOrder = true;
        await _host.Dispatcher.PublishAsync(Notification);
        if (!state.HandlerOrder.SequenceEqual(Enumerable.Range(1, HandlerCount)))
        {
            throw new InvalidOperationException("Notification fan-out validation failed.");
        }

        state.ValidateOrder = false;
        state.HandlerOrder.Clear();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _host.Dispose();
        _provider.Dispose();
    }

    [Benchmark]
    public ValueTask Publish() =>
        _host.Dispatcher.PublishAsync(Notification);
}