using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Model;

internal sealed class GenerationModel
{
    internal static readonly GenerationModel Empty = new();

    private GenerationModel()
    {
    }

    internal GenerationModel(
        string? handlerRegistrationMethod,
        string? dispatcherRegistrationMethod,
        ImmutableArray<HandlerDefinition> localHandlers,
        ImmutableArray<HandlerDefinition> dispatchHandlers,
        ImmutableArray<OpenNotificationHandlerDefinition> localOpenNotificationHandlers,
        ImmutableArray<OpenNotificationHandlerDefinition> dispatchOpenNotificationHandlers,
        ImmutableArray<DispatchRoute> routes,
        ImmutableArray<INamedTypeSymbol> pipelineBehaviors,
        ImmutableArray<Diagnostic> diagnostics,
        string assemblyName = "",
        INamedTypeSymbol? unitType = null)
    {
        HandlerRegistrationMethod = handlerRegistrationMethod;
        DispatcherRegistrationMethod = dispatcherRegistrationMethod;
        LocalHandlers = localHandlers;
        DispatchHandlers = dispatchHandlers;
        LocalOpenNotificationHandlers = localOpenNotificationHandlers;
        DispatchOpenNotificationHandlers = dispatchOpenNotificationHandlers;
        Routes = routes;
        PipelineBehaviors = pipelineBehaviors;
        Diagnostics = diagnostics;
        AssemblyName = assemblyName;
        UnitType = unitType;
    }

    internal string? HandlerRegistrationMethod { get; }
    internal string? DispatcherRegistrationMethod { get; }
    internal ImmutableArray<HandlerDefinition> LocalHandlers { get; } = [];
    internal ImmutableArray<HandlerDefinition> DispatchHandlers { get; } = [];
    internal ImmutableArray<OpenNotificationHandlerDefinition> LocalOpenNotificationHandlers { get; } = [];
    internal ImmutableArray<OpenNotificationHandlerDefinition> DispatchOpenNotificationHandlers { get; } = [];
    internal ImmutableArray<DispatchRoute> Routes { get; } = [];
    internal ImmutableArray<INamedTypeSymbol> PipelineBehaviors { get; } = [];
    internal ImmutableArray<Diagnostic> Diagnostics { get; } = [];
    internal string AssemblyName { get; } = "";
    internal INamedTypeSymbol? UnitType { get; }
}