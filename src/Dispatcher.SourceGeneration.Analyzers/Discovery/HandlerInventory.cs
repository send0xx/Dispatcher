using System.Collections.Immutable;
using Dispatcher.SourceGeneration.Model;

namespace Dispatcher.SourceGeneration.Discovery;

internal sealed class HandlerInventory(
    ImmutableArray<HandlerDefinition> closedHandlers,
    ImmutableArray<OpenNotificationHandlerDefinition> openNotificationHandlers)
{
    internal ImmutableArray<HandlerDefinition> ClosedHandlers { get; } = closedHandlers;
    internal ImmutableArray<OpenNotificationHandlerDefinition> OpenNotificationHandlers { get; } =
        openNotificationHandlers;
}