using Dispatcher.DependencyInjection;
using Dispatcher.Parity.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Parity.Tests;

/// <summary>
/// Runs the shared dispatch scenarios against the reflection-based implementation, which discovers
/// the parity handlers by scanning this assembly.
/// </summary>
public sealed class ReflectionDispatchParityTests : DispatchParityTests
{
    private protected override void Register(IServiceCollection services)
    {
        services.AddDispatcher();
        services.AddDispatcherHandlers<ReflectionDispatchParityTests>();
        services.AddPipelineBehavior<GreetBehavior>();
        services.AddPipelineBehavior<FirstOrderedBehavior>();
        services.AddPipelineBehavior<SecondOrderedBehavior>();
        services.AddPipelineBehavior<CachedQueryBehavior>();
        services.AddPipelineBehavior<TrackedCommandBehavior>();
    }
}