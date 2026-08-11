using BenchmarkDotNet.Attributes;
using Dispatcher;
using Dispatcher.DependencyInjection;
using Dispatcher.SourceGeneration;
using Microsoft.Extensions.DependencyInjection;

[assembly: GenerateDispatcherHandlers("AddGeneratedBenchmarkHandlers")]
[assembly: GenerateDispatcher("AddGeneratedBenchmarkDispatcher")]

namespace Dispatcher.Benchmarks;

[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "RatioSD")]
public class ImplementationBenchmarks
{
    private static readonly PingQuery QueryMessage = new(41);
    private static readonly IncrementCommand CommandWithResponse = new(41);
    private static readonly TouchCommand Command = new();
    private static readonly TouchedNotification Notification = new();
    private static readonly TouchedTwiceNotification MultiHandlerNotification = new();

    private ServiceProvider _reflectionProvider = null!;
    private ServiceProvider _generatedProvider = null!;
    private IServiceScope _reflectionScope = null!;
    private IServiceScope _generatedScope = null!;
    private IDispatcher _reflectionDispatcher = null!;
    private IDispatcher _generatedDispatcher = null!;

    [GlobalSetup]
    public void Setup()
    {
        var reflectionServices = new ServiceCollection();
        reflectionServices
            .AddDispatcher()
            .AddDispatcherHandlers<ImplementationBenchmarks>();
        _reflectionProvider = reflectionServices.BuildServiceProvider();
        _reflectionScope = _reflectionProvider.CreateScope();
        _reflectionDispatcher = _reflectionScope.ServiceProvider.GetRequiredService<IDispatcher>();

        var generatedServices = new ServiceCollection();
        generatedServices
            .AddGeneratedBenchmarkHandlers()
            .AddGeneratedBenchmarkDispatcher();
        _generatedProvider = generatedServices.BuildServiceProvider();
        _generatedScope = _generatedProvider.CreateScope();
        _generatedDispatcher = _generatedScope.ServiceProvider.GetRequiredService<IDispatcher>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _reflectionScope.Dispose();
        _generatedScope.Dispose();
        _reflectionProvider.Dispose();
        _generatedProvider.Dispose();
    }

    [Benchmark(Baseline = true)]
    public ValueTask<int> ReflectionQuery() => _reflectionDispatcher.QueryAsync(QueryMessage);

    [Benchmark]
    public ValueTask<int> GeneratedQuery() => _generatedDispatcher.QueryAsync(QueryMessage);

    [Benchmark]
    public ValueTask<int> ReflectionCommandWithResponse() =>
        _reflectionDispatcher.ExecuteAsync(CommandWithResponse);

    [Benchmark]
    public ValueTask<int> GeneratedCommandWithResponse() =>
        _generatedDispatcher.ExecuteAsync(CommandWithResponse);

    [Benchmark]
    public ValueTask ReflectionCommand() => _reflectionDispatcher.ExecuteAsync(Command);

    [Benchmark]
    public ValueTask GeneratedCommand() => _generatedDispatcher.ExecuteAsync(Command);

    [Benchmark]
    public ValueTask ReflectionNotification() => _reflectionDispatcher.PublishAsync(Notification);

    [Benchmark]
    public ValueTask GeneratedNotification() => _generatedDispatcher.PublishAsync(Notification);

    [Benchmark]
    public ValueTask ReflectionNotificationWithTwoHandlers() =>
        _reflectionDispatcher.PublishAsync(MultiHandlerNotification);

    [Benchmark]
    public ValueTask GeneratedNotificationWithTwoHandlers() =>
        _generatedDispatcher.PublishAsync(MultiHandlerNotification);
}

[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "RatioSD")]
public class ImplementationPipelineBenchmarks
{
    private static readonly PingQuery QueryMessage = new(41);

    private ServiceProvider _reflectionProvider = null!;
    private ServiceProvider _generatedProvider = null!;
    private IServiceScope _reflectionScope = null!;
    private IServiceScope _generatedScope = null!;
    private IDispatcher _reflectionDispatcher = null!;
    private IDispatcher _generatedDispatcher = null!;

    [GlobalSetup]
    public void Setup()
    {
        var reflectionServices = new ServiceCollection();
        reflectionServices
            .AddDispatcher()
            .AddDispatcherHandlers<ImplementationPipelineBenchmarks>()
            .AddPipelineBehavior<PassthroughBehavior<PingQuery, int>>();
        _reflectionProvider = reflectionServices.BuildServiceProvider();
        _reflectionScope = _reflectionProvider.CreateScope();
        _reflectionDispatcher = _reflectionScope.ServiceProvider.GetRequiredService<IDispatcher>();

        var generatedServices = new ServiceCollection();
        generatedServices
            .AddGeneratedBenchmarkHandlers()
            .AddGeneratedBenchmarkDispatcher()
            .AddPipelineBehavior<PingQuery, int, PassthroughBehavior<PingQuery, int>>();
        _generatedProvider = generatedServices.BuildServiceProvider();
        _generatedScope = _generatedProvider.CreateScope();
        _generatedDispatcher = _generatedScope.ServiceProvider.GetRequiredService<IDispatcher>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _reflectionScope.Dispose();
        _generatedScope.Dispose();
        _reflectionProvider.Dispose();
        _generatedProvider.Dispose();
    }

    [Benchmark(Baseline = true)]
    public ValueTask<int> ReflectionQuery() => _reflectionDispatcher.QueryAsync(QueryMessage);

    [Benchmark]
    public ValueTask<int> GeneratedQuery() => _generatedDispatcher.QueryAsync(QueryMessage);
}