using BenchmarkDotNet.Attributes;
using Dispatcher.Benchmarks.Shared;

namespace Dispatcher.Benchmarks.Scale;

[ScaleOperationBenchmark]
public class SourceGenerationScaleBenchmarks : ScaleBenchmarkBase
{
    public override void Setup()
    {
        base.Setup();
        _ = FixtureCompiler.RunGenerator(Corpus.ChangedHostBaseDriver, Corpus.ChangedHostCompilation);
        _ = FixtureCompiler.RunGenerator(Corpus.ModuleReferenceBaseDriver, Corpus.HostCompilation);
    }

    [Benchmark]
    public int ColdModuleHandlerRegistration()
    {
        var result = FixtureCompiler.RunColdGenerator(Corpus.ModuleCompilations[0]);
        return result.Result.GeneratedTrees.Length;
    }

    [Benchmark]
    public int ColdHostDispatcherGeneration()
    {
        var result = FixtureCompiler.RunColdGenerator(Corpus.HostCompilation);
        return result.Result.GeneratedTrees.Length;
    }

    [Benchmark]
    public int TotalColdGeneration()
    {
        var generatedTreeCount = 0;
        foreach (var module in Corpus.ModuleCompilations)
        {
            generatedTreeCount += FixtureCompiler.RunColdGenerator(module).Result.GeneratedTrees.Length;
        }

        generatedTreeCount += FixtureCompiler.RunColdGenerator(Corpus.HostCompilation).Result.GeneratedTrees.Length;
        return generatedTreeCount;
    }

    [Benchmark]
    public int CachedIncrementalGeneration()
    {
        var result = FixtureCompiler.RunGenerator(Corpus.CachedHostDriver, Corpus.HostCompilation);
        return result.Result.GeneratedTrees.Length;
    }

    [Benchmark]
    public int IncrementalAfterMessageChange()
    {
        var result = FixtureCompiler.RunGenerator(
            Corpus.ChangedHostBaseDriver,
            Corpus.ChangedHostCompilation);
        return result.Result.GeneratedTrees.Length;
    }

    [Benchmark]
    public int IncrementalAfterModuleReference()
    {
        var result = FixtureCompiler.RunGenerator(
            Corpus.ModuleReferenceBaseDriver,
            Corpus.HostCompilation);
        return result.Result.GeneratedTrees.Length;
    }

    [Benchmark]
    public long EmitUpdatedHostAssembly()
    {
        var generation = FixtureCompiler.RunGenerator(
            Corpus.ChangedHostBaseDriver,
            Corpus.ChangedHostCompilation);
        return FixtureCompiler.EmitToMemory(generation.OutputCompilation);
    }
}