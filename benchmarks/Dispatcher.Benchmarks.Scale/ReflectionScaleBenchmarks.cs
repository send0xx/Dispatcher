using BenchmarkDotNet.Attributes;
using Dispatcher.Benchmarks.Shared;
using Dispatcher.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks.Scale;

[ScaleOperationBenchmark]
public class ReflectionScaleBenchmarks : ScaleBenchmarkBase
{
    private ServiceDescriptor[] _scannedDescriptors = null!;

    public override void Setup()
    {
        base.Setup();
        _scannedDescriptors = ScanModules().ToArray();
    }

    public override void Cleanup()
    {
        _scannedDescriptors = [];
        base.Cleanup();
    }

    [Benchmark]
    public int HandlerScanning()
    {
        var services = ScanModules();
        return services.Count;
    }

    [Benchmark]
    public int RegistryCreationFromCompletedServices()
    {
        var services = new ServiceCollection();
        foreach (var descriptor in _scannedDescriptors)
        {
            ((ICollection<ServiceDescriptor>)services).Add(descriptor);
        }

        services.AddDispatcher();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IDispatcher>();
        return services.Count;
    }

    [Benchmark]
    public int CompleteProviderStartup()
    {
        var services = ScanModules();
        services.AddDispatcher();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IDispatcher>();
        return services.Count;
    }

    private ServiceCollection ScanModules()
    {
        var services = new ServiceCollection();
        foreach (var module in Corpus.LoadedModules)
        {
            services.AddDispatcherHandlers(module);
        }

        return services;
    }
}