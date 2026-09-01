using BenchmarkDotNet.Running;
using Dispatcher.Benchmarks.Execution;
using Dispatcher.Benchmarks.Shared;

if (args.Length > 0 && args[0].Equals("validate", StringComparison.OrdinalIgnoreCase))
{
    foreach (var implementation in Enum.GetValues<BenchmarkImplementation>())
    {
        var basic = new BasicDispatchBenchmarks { Implementation = implementation };
        await basic.Setup();
        basic.Cleanup();

        var pipeline = new PipelineDepthBenchmarks { Implementation = implementation, BehaviorCount = 10 };
        await pipeline.Setup();
        pipeline.Cleanup();

        var fanOut = new NotificationFanOutBenchmarks { Implementation = implementation, HandlerCount = 50 };
        await fanOut.Setup();
        fanOut.Cleanup();
    }

    Console.WriteLine("Execution benchmark validation passed for both implementations.");
    return;
}

var profiles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["quick"] = ["*BasicDispatchBenchmarks*", "*PipelineDepthBenchmarks*"],
    ["execution"] = ["*"],
    ["full"] = ["*"]
};

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
    .Run(BenchmarkProfiles.Select(args, profiles));