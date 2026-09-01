using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Dispatcher.Benchmarks.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks.Scale;

[DispatcherBenchmark]
public class SourceGenerationScaleBenchmarks : ScaleBenchmarkBase
{
    private ServiceProvider _sampleProvider = null!;
    private IServiceScope _sampleScope = null!;
    private IDispatcher _sampleDispatcher = null!;

    public override void Setup()
    {
        base.Setup();
        _sampleProvider = (ServiceProvider)Corpus.BuildGeneratedProvider();
        _sampleScope = _sampleProvider.CreateScope();
        _sampleDispatcher = _sampleScope.ServiceProvider.GetRequiredService<IDispatcher>();

        _ = FixtureCompiler.RunGenerator(Corpus.ChangedHostBaseDriver, Corpus.ChangedHostCompilation);
        _ = FixtureCompiler.RunGenerator(Corpus.ModuleReferenceBaseDriver, Corpus.HostCompilation);
    }

    public override void Cleanup()
    {
        DisposeGeneratedRuntime();
        base.Cleanup();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void DisposeGeneratedRuntime()
    {
        _sampleScope.Dispose();
        _sampleProvider.Dispose();
        _sampleDispatcher = null!;
        _sampleScope = null!;
        _sampleProvider = null!;
    }

    [Benchmark, InvocationCount(1)]
    public int ColdModuleHandlerRegistration()
    {
        var result = FixtureCompiler.RunGenerator(Corpus.ModuleCompilations[0]);
        return result.Result.GeneratedTrees.Length;
    }

    [Benchmark, InvocationCount(1)]
    public int ColdHostDispatcherGeneration()
    {
        var result = FixtureCompiler.RunGenerator(Corpus.HostCompilation);
        return result.Result.GeneratedTrees.Length;
    }

    [Benchmark, InvocationCount(1)]
    public int TotalColdGeneration()
    {
        var generatedTreeCount = 0;
        foreach (var module in Corpus.ModuleCompilations)
        {
            generatedTreeCount += FixtureCompiler.RunGenerator(module).Result.GeneratedTrees.Length;
        }

        generatedTreeCount += FixtureCompiler.RunGenerator(Corpus.HostCompilation).Result.GeneratedTrees.Length;
        return generatedTreeCount;
    }

    [Benchmark, InvocationCount(1)]
    public int CachedIncrementalGeneration()
    {
        var result = FixtureCompiler.RunGenerator(Corpus.CachedHostDriver, Corpus.HostCompilation);
        return result.Result.GeneratedTrees.Length;
    }

    [Benchmark, InvocationCount(1)]
    public int IncrementalAfterMessageChange()
    {
        var result = FixtureCompiler.RunGenerator(
            Corpus.ChangedHostBaseDriver,
            Corpus.ChangedHostCompilation);
        return result.Result.GeneratedTrees.Length;
    }

    [Benchmark, InvocationCount(1)]
    public int IncrementalAfterModuleReference()
    {
        var result = FixtureCompiler.RunGenerator(
            Corpus.ModuleReferenceBaseDriver,
            Corpus.HostCompilation);
        return result.Result.GeneratedTrees.Length;
    }

    [Benchmark, InvocationCount(1)]
    public long EmitUpdatedHostAssembly()
    {
        var generation = FixtureCompiler.RunGenerator(
            Corpus.ChangedHostBaseDriver,
            Corpus.ChangedHostCompilation);
        return FixtureCompiler.EmitToMemory(generation.OutputCompilation);
    }

    [Benchmark]
    public ValueTask<int> SampledGeneratedDispatch() => Corpus.DispatchSamplesAsync(_sampleDispatcher);
}