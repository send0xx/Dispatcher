using Dispatcher.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection.Tests.TestSupport;

internal static class TestServices
{
    internal static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddDispatcher();
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        services.AddScoped<TestState>();
        return services;
    }

    internal static ServiceProvider CreateProvider() =>
        BuildProvider(CreateServices());

    internal static ServiceProvider BuildProvider(IServiceCollection services) =>
        services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
}

internal sealed class TestAssemblyMarker;

internal sealed class TestState
{
    internal string? Recorded { get; set; }
    internal List<string> Events { get; } = [];
    internal TaskCompletionSource<string> DelayedQueryCompletion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int BehaviorInstances { get; set; }
}