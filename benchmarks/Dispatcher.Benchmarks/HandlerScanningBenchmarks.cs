using BenchmarkDotNet.Attributes;
using Dispatcher.DependencyInjection;
using Dispatcher.SampleApi.Modules.Orders;
using Dispatcher.SampleApi.Modules.Stock;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks;

[MemoryDiagnoser]
[HideColumns("Error", "StdDev")]
public class HandlerScanningBenchmarks
{
    [Benchmark]
    public int ScanModulesWithSharedContractsSeparately()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers(typeof(OrdersModule).Assembly);
        services.AddDispatcherHandlers(typeof(StockModule).Assembly);
        return services.Count;
    }
}