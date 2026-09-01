using BenchmarkDotNet.Attributes;
using Dispatcher.Benchmarks.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks.Execution;

[DispatcherBenchmark]
public class PipelineDepthBenchmarks
{
    private static readonly PipelineQuery Query = new(41);
    private BenchmarkProvider _provider = null!;
    private BenchmarkHost _host = null!;

    [ParamsAllValues] public BenchmarkImplementation Implementation { get; set; }

    [Params(0, 1, 3, 5, 10)] public int BehaviorCount { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        _provider = ExecutionHostFactory.Create(
            Implementation,
            services => PipelineRegistration.Add(services, BehaviorCount));
        _host = _provider.CreateHost();

        var state = _host.Services.GetRequiredService<PipelineState>();
        state.ValidateOrder = true;
        var result = await _host.Dispatcher.QueryAsync(Query, CancellationToken.None);
        var expected = Enumerable.Range(1, BehaviorCount)
            .Concat(Enumerable.Range(1, BehaviorCount).Reverse().Select(static value => -value));
        if (result != 41 || !state.Order.SequenceEqual(expected) ||
            BehaviorCount > 0 && state.HandlerToken != state.ReplacementToken)
        {
            throw new InvalidOperationException("Pipeline setup validation failed.");
        }

        state.ValidateOrder = false;
        state.Order.Clear();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _host.Dispose();
        _provider.Dispose();
    }

    [Benchmark]
    public ValueTask<int> QueryWithBehaviors() => _host.Dispatcher.QueryAsync(Query);
}