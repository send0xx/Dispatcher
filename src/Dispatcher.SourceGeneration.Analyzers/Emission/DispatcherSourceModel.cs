using Dispatcher.SourceGeneration.Model;

namespace Dispatcher.SourceGeneration.Emission;

internal sealed class DispatcherSourceModel
{
    internal DispatcherSourceModel(GenerationModel generation)
    {
        Queries = generation.Routes.Where(static route => route.Handler?.Kind == HandlerKind.Query).ToArray();
        ResponseCommands = generation.Routes
            .Where(static route => route.Handler?.Kind == HandlerKind.CommandWithResponse)
            .ToArray();
        Commands = generation.Routes.Where(static route => route.Handler?.Kind == HandlerKind.Command).ToArray();
        Notifications = generation.Routes
            .Where(static route => route.Handler?.Kind == HandlerKind.Notification ||
                                   !route.OpenNotificationHandlers.IsDefaultOrEmpty)
            .ToArray();
        RoutedOpenHandlers = Notifications
            .SelectMany(static route => route.OpenNotificationHandlers)
            .GroupBy(static handler => handler.SortKey, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();
    }

    internal DispatchRoute[] Queries { get; }
    internal DispatchRoute[] ResponseCommands { get; }
    internal DispatchRoute[] Commands { get; }
    internal DispatchRoute[] Notifications { get; }
    internal OpenNotificationHandlerDefinition[] RoutedOpenHandlers { get; }
    internal bool UsesOpenNotificationHandlers => RoutedOpenHandlers.Length > 0;
}