using Dispatcher.Benchmarks.Shared;
using Dispatcher.SourceGeneration;
using Microsoft.Extensions.DependencyInjection;

[assembly: GenerateDispatcher("AddGeneratedExecutionDispatcher")]

namespace Dispatcher.Benchmarks.Execution;

internal static class ExecutionHostFactory
{
    internal static BenchmarkProvider Create(
        BenchmarkImplementation implementation,
        Action<IServiceCollection>? configureHandlers = null,
        Action<DispatcherOptions>? configureOptions = null) =>
        BenchmarkProvider.Create(
            implementation,
            static (services, configure) => services.AddGeneratedExecutionDispatcher(configure),
            configureHandlers,
            configureOptions);
}