using BenchmarkDotNet.Attributes;
using Dispatcher.Benchmarks.Shared;
using Dispatcher.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks.Scale;

[DispatcherBenchmark]
public class ReflectionScaleBenchmarks : ScaleBenchmarkBase
{
    private ServiceDescriptor[] _scannedDescriptors = null!;
    private ServiceProvider _sampleProvider = null!;
    private IServiceScope _sampleScope = null!;
    private IDispatcher _sampleDispatcher = null!;

    public override void Setup()
    {
        base.Setup();
        var services = ScanModules();
        _scannedDescriptors = services.ToArray();
        _sampleProvider = Corpus.BuildReflectionProvider();
        _sampleScope = _sampleProvider.CreateScope();
        _sampleDispatcher = _sampleScope.ServiceProvider.GetRequiredService<IDispatcher>();
    }

    public override void Cleanup()
    {
        _sampleScope.Dispose();
        _sampleProvider.Dispose();
        _sampleDispatcher = null!;
        _sampleScope = null!;
        _sampleProvider = null!;
        _scannedDescriptors = [];
        base.Cleanup();
    }

    [Benchmark, InvocationCount(1)]
    public int HandlerScanning()
    {
        var services = ScanModules();
        return services.Count;
    }

    [Benchmark, InvocationCount(1)]
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

    [Benchmark, InvocationCount(1)]
    public int CompleteProviderStartup()
    {
        var services = ScanModules();
        services.AddDispatcher();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IDispatcher>();
        return services.Count;
    }

    [Benchmark]
    public ValueTask<int> SampledDispatch() => Corpus.DispatchSamplesAsync(_sampleDispatcher);

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