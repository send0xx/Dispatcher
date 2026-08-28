using System.Reflection;
using System.Reflection.Emit;
using BenchmarkDotNet.Attributes;
using Dispatcher.DependencyInjection;
using Dispatcher.SampleApi.Modules.Orders;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks;

/// <summary>
/// Measures reflection assembly scanning at startup, over a real module assembly and a growing
/// number of emitted ones, as a modular monolith registers them.
/// </summary>
/// <remarks>
/// <see cref="ScanUnroutableModules"/> covers modules whose messages no handler routes, so they stay
/// under consideration for every later scan. <see cref="ScanRoutableModules"/> covers modules whose
/// messages all route to a handled base type and enter the scanned route-target catalog.
/// <see cref="BuildRegistryForUnroutableModules"/> measures the complete startup path when pending
/// targets remain unchanged at registry creation.
/// <see cref="BuildRegistryWithUnmatchedOpenHandler"/> measures registry creation when an open
/// notification handler cannot be closed over any discovered notification.
/// </remarks>
[MemoryDiagnoser]
[HideColumns("Error", "StdDev")]
public class AssemblyScanningBenchmarks
{
    private const int MessageTypesPerModule = 250;

    private Assembly[] _modules = [];

    [Params(4, 16)]
    public int ModuleCount { get; set; }

    [GlobalSetup]
    public void Setup() =>
        _modules = Enumerable.Range(0, ModuleCount).Select(CreateModule).ToArray();

    [Benchmark]
    public int ScanUnroutableModules()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers(typeof(OrdersModule).Assembly);
        return ScanAll(services);
    }

    [Benchmark]
    public int ScanRoutableModules()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers(typeof(OrdersModule).Assembly);
        services.AddSingleton<INotificationHandler<ModuleEvent>>(_ => null!);
        return ScanAll(services);
    }

    /// <summary>
    /// Measures the whole startup path for the unroutable case, including registry creation.
    /// </summary>
    [Benchmark]
    public int BuildRegistryForUnroutableModules()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers(typeof(OrdersModule).Assembly);
        ScanAll(services);
        services.AddDispatcher();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IDispatcher>();
        return services.Count;
    }

    /// <summary>
    /// Measures registry creation when every attempt to close an open notification handler fails
    /// its generic constraints.
    /// </summary>
    [Benchmark]
    public int BuildRegistryWithUnmatchedOpenHandler()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers(typeof(OrdersModule).Assembly);
        ScanAll(services);
        services.AddNotificationHandler(typeof(UnmatchedOpenNotificationHandler<>));
        services.AddDispatcher();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IDispatcher>();
        return services.Count;
    }

    private int ScanAll(ServiceCollection services)
    {
        foreach (var module in _modules)
        {
            services.AddDispatcherHandlers(module);
        }

        return services.Count;
    }

    private static Assembly CreateModule(int index)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"Dispatcher.Benchmarks.Module{index}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("main");
        for (var messageIndex = 0; messageIndex < MessageTypesPerModule; messageIndex++)
        {
            module.DefineType(
                    $"Module{index}.Notification{messageIndex}",
                    TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class,
                    typeof(ModuleEvent))
                .CreateType();
        }

        return assembly;
    }
}