using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Dispatcher.Benchmarks.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks.Scale;

[DispatcherBenchmark]
public class ScaleDispatchBenchmarks : ScaleBenchmarkBase
{
    private ServiceProvider _provider = null!;
    private IServiceScope _scope = null!;
    private IDispatcher _dispatcher = null!;

    [ParamsAllValues]
    public BenchmarkImplementation Implementation { get; set; }

    public override void Setup()
    {
        base.Setup();
        _provider = Implementation == BenchmarkImplementation.Reflection
            ? Corpus.BuildReflectionProvider()
            : (ServiceProvider)Corpus.BuildGeneratedProvider();
        _scope = _provider.CreateScope();
        _dispatcher = _scope.ServiceProvider.GetRequiredService<IDispatcher>();
    }

    public override void Cleanup()
    {
        DisposeDispatchRuntime();
        base.Cleanup();
    }

    [Benchmark]
    public ValueTask<int> SampledDispatch() => Corpus.DispatchSamplesAsync(_dispatcher);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DisposeDispatchRuntime()
    {
        _scope.Dispose();
        _provider.Dispose();
        _dispatcher = null!;
        _scope = null!;
        _provider = null!;
    }
}