using Dispatcher.SourceGeneration;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Parity.Tests;

/// <summary>
/// Runs the shared dispatch scenarios against the source-generated implementation, which registers
/// the same parity handlers from metadata emitted at compile time.
/// </summary>
public sealed class GeneratedDispatchParityTests : DispatchParityTests
{
    private protected override void Register(IServiceCollection services)
    {
        services.AddGeneratedDispatcher();
        services.AddParityHandlers();

        // A closed behavior registers through the typed method in the core package, which needs no
        // runtime generic construction. The generator emits AddPipelineBehavior only for the open
        // generic shape, which cannot be registered that way.
        services.AddPipelineBehavior<GreetQuery, string, GreetBehavior>();
    }
}