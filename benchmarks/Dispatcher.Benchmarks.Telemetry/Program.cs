using BenchmarkDotNet.Running;
using Dispatcher.Benchmarks.Shared;
using Dispatcher.Benchmarks.Telemetry;

if (args.Length > 0 && args[0].Equals("quick", StringComparison.OrdinalIgnoreCase))
{
    Environment.SetEnvironmentVariable("DISPATCHER_BENCHMARK_PROFILE", "quick");
}

if (args.Length > 0 && args[0].Equals("validate", StringComparison.OrdinalIgnoreCase))
{
    foreach (var implementation in Enum.GetValues<BenchmarkImplementation>())
    {
        foreach (var mode in Enum.GetValues<TelemetryMode>())
        {
            foreach (var outcome in Enum.GetValues<OperationOutcome>())
            {
                var benchmark = new TelemetryBenchmarks
                {
                    Implementation = implementation,
                    Mode = mode,
                    Outcome = outcome
                };
                await benchmark.Setup();
                benchmark.Cleanup();
            }
        }
    }

    Console.WriteLine("Telemetry benchmark validation passed for both implementations.");
    return;
}

var profiles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["quick"] = ["*TelemetryBenchmarks*"],
    ["telemetry"] = ["*"],
    ["full"] = ["*"]
};

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
    .Run(BenchmarkProfiles.Select(args, profiles));