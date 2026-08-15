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
/// messages all route to a handled base type, so the service collection grows with every scan.
/// <see cref="BuildRegistryForUnroutableModules"/> adds the registry creation that follows, which is
/// where the messages scanning left pending are reconsidered.
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
        services.AddSingleton<HandlerRegistration>(
            new NotificationHandlerRegistration(typeof(ModuleEvent), typeof(ModuleEventHandler)));
        return ScanAll(services);
    }

    /// <summary>
    /// Measures the whole startup path for the unroutable case: scanning, and then the registry
    /// creation that reconsiders every message scanning left pending.
    /// </summary>
    [Benchmark]
    public int BuildRegistryForUnroutableModules()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers(typeof(OrdersModule).Assembly);
        ScanAll(services);
        services.AddDispatcher();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<DispatcherRegistry>();
        return services.Count;
    }

    private int ScanAll(IServiceCollection services)
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

    /// <summary>
    /// A handled base notification for the emitted module messages. It is abstract, so scanning the
    /// benchmark assembly itself never treats it as a concrete route target.
    /// </summary>
    public abstract class ModuleEvent : INotification;

    /// <summary>
    /// Stands in for the handler of <see cref="ModuleEvent"/>. Registration metadata is all the
    /// scanner reads, so this type implements no handler interface and is never activated.
    /// </summary>
    private sealed class ModuleEventHandler;
}