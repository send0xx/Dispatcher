using BenchmarkDotNet.Attributes;

namespace Dispatcher.Benchmarks.Scale;

public abstract class ScaleBenchmarkBase
{
    private protected FixtureCorpus Corpus { get; private set; } = null!;

    [ParamsSource(nameof(Sizes))] public FixtureSize Size { get; set; }

    public static IEnumerable<FixtureSize> Sizes =>
        Environment.GetEnvironmentVariable("DISPATCHER_BENCHMARK_PROFILE") == "quick"
            ? [FixtureSize.Small]
            : Enum.GetValues<FixtureSize>();

    [GlobalSetup]
    public virtual void Setup()
    {
        Corpus = FixtureCorpus.Create(Size);
        var expectedTrees = Corpus.Configuration.ModuleCount + 2;
        var expectedAssemblies = Corpus.Configuration.ModuleCount + 2;
        if (Corpus.ModuleTrees.Length + 2 != expectedTrees ||
            Corpus.ModuleAssemblyPaths.Length + 2 != expectedAssemblies)
        {
            throw new InvalidOperationException("Scale fixture tree or assembly counts are invalid.");
        }
    }

    [GlobalCleanup]
    public virtual void Cleanup()
    {
        Corpus.Dispose();
        if (!Corpus.LoadContextUnloaded)
        {
            throw new InvalidOperationException("The collectible scale fixture load context did not unload.");
        }
    }
}