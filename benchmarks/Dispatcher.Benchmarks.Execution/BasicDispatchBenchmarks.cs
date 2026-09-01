using BenchmarkDotNet.Attributes;
using Dispatcher.Benchmarks.Shared;

namespace Dispatcher.Benchmarks.Execution;

[DispatcherBenchmark]
public class BasicDispatchBenchmarks
{
    private static readonly PingQuery QueryMessage = new(41);
    private static readonly IncrementCommand ResponseCommand = new(41);
    private static readonly TouchCommand Command = new();
    private static readonly TouchedNotification Notification = new();
    private static readonly TouchedTwiceNotification TwoHandlerNotification = new();

    private readonly DirectPingHandler _directHandler = new();
    private BenchmarkProvider _provider = null!;
    private BenchmarkHost _host = null!;

    [ParamsAllValues]
    public BenchmarkImplementation Implementation { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _provider = ExecutionHostFactory.Create(Implementation);
        _host = _provider.CreateHost();
        if (await _host.Dispatcher.QueryAsync(QueryMessage) != 42)
        {
            throw new InvalidOperationException("Dispatcher validation failed.");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _host.Dispose();
        _provider.Dispose();
    }

    [Benchmark(Baseline = true)]
    public ValueTask<int> DirectQueryHandler() =>
        _directHandler.HandleAsync(QueryMessage, CancellationToken.None);

    [Benchmark]
    public ValueTask<int> Query() => _host.Dispatcher.QueryAsync(QueryMessage);

    [Benchmark]
    public ValueTask<int> CommandReturningResponse() =>
        _host.Dispatcher.ExecuteAsync(ResponseCommand);

    [Benchmark]
    public ValueTask CommandWithoutResponse() => _host.Dispatcher.ExecuteAsync(Command);

    [Benchmark]
    public ValueTask NotificationWithOneHandler() => _host.Dispatcher.PublishAsync(Notification);

    [Benchmark]
    public ValueTask NotificationWithTwoHandlers() =>
        _host.Dispatcher.PublishAsync(TwoHandlerNotification);
}