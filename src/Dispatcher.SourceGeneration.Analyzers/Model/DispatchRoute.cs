using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Model;

internal sealed class DispatchRoute(
    INamedTypeSymbol messageType,
    HandlerDefinition? handler,
    ImmutableArray<OpenNotificationHandlerDefinition> openNotificationHandlers)
{
    internal INamedTypeSymbol MessageType { get; } = messageType;
    internal HandlerDefinition? Handler { get; } = handler;
    internal ImmutableArray<OpenNotificationHandlerDefinition> OpenNotificationHandlers { get; } =
        openNotificationHandlers;
}