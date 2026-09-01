using BenchmarkDotNet.Running;
using Dispatcher.Benchmarks.Scale;
using Dispatcher.Benchmarks.Shared;

if (args.Length > 0 && args[0].Equals("build-timing", StringComparison.OrdinalIgnoreCase))
{
    var size = args.Length > 1 && Enum.TryParse<FixtureSize>(args[1], ignoreCase: true, out var selected)
        ? selected
        : FixtureSize.Small;
    await EndToEndBuildTiming.RunAsync(size);
    return;
}

if (args.Length > 0 && args[0].Equals("validate", StringComparison.OrdinalIgnoreCase))
{
    var reflection = new ReflectionScaleBenchmarks { Size = FixtureSize.Small };
    reflection.Setup();
    reflection.Cleanup();

    var generation = new SourceGenerationScaleBenchmarks { Size = FixtureSize.Small };
    generation.Setup();
    generation.Cleanup();

    foreach (var implementation in Enum.GetValues<BenchmarkImplementation>())
    {
        var dispatch = new ScaleDispatchBenchmarks
        {
            Size = FixtureSize.Small,
            Implementation = implementation
        };
        dispatch.Setup();
        dispatch.Cleanup();
    }

    Console.WriteLine("Small scale fixture, providers, and load-context cleanup validation passed for every group.");
    return;
}

if (args.Length > 0 && args[0].Equals("quick", StringComparison.OrdinalIgnoreCase))
{
    Environment.SetEnvironmentVariable("DISPATCHER_BENCHMARK_PROFILE", "quick");
}

var profiles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["quick"] =
    [
        "*ReflectionScaleBenchmarks*",
        "*SourceGenerationScaleBenchmarks*",
        "*ScaleDispatchBenchmarks*"
    ],
    ["scale"] = ["*"],
    ["full"] = ["*"]
};

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
    .Run(BenchmarkProfiles.Select(args, profiles));