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
    var benchmark = new SourceGenerationScaleBenchmarks { Size = FixtureSize.Small };
    benchmark.Setup();
    benchmark.Cleanup();
    Console.WriteLine("Small scale fixture and generated-provider cleanup validation passed.");
    return;
}

if (args.Length > 0 && args[0].Equals("quick", StringComparison.OrdinalIgnoreCase))
{
    Environment.SetEnvironmentVariable("DISPATCHER_BENCHMARK_PROFILE", "quick");
}

var profiles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["quick"] = ["*ReflectionScaleBenchmarks*", "*SourceGenerationScaleBenchmarks*"],
    ["scale"] = ["*"],
    ["full"] = ["*"]
};

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
    .Run(BenchmarkProfiles.Select(args, profiles));