using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Discovery;

internal sealed class DispatcherSymbols
{
    private DispatcherSymbols(
        INamedTypeSymbol queryHandler,
        INamedTypeSymbol commandHandler,
        INamedTypeSymbol resultlessCommandHandler,
        INamedTypeSymbol notificationHandler,
        INamedTypeSymbol? query,
        INamedTypeSymbol? command,
        INamedTypeSymbol? resultlessCommand,
        INamedTypeSymbol? request,
        INamedTypeSymbol? notification,
        INamedTypeSymbol? pipelineBehavior,
        INamedTypeSymbol? unit)
    {
        QueryHandler = queryHandler;
        CommandHandler = commandHandler;
        ResultlessCommandHandler = resultlessCommandHandler;
        NotificationHandler = notificationHandler;
        Query = query;
        Command = command;
        ResultlessCommand = resultlessCommand;
        Request = request;
        Notification = notification;
        PipelineBehavior = pipelineBehavior;
        Unit = unit;
    }

    internal INamedTypeSymbol QueryHandler { get; }
    internal INamedTypeSymbol CommandHandler { get; }
    internal INamedTypeSymbol ResultlessCommandHandler { get; }
    internal INamedTypeSymbol NotificationHandler { get; }
    internal INamedTypeSymbol? Query { get; }
    internal INamedTypeSymbol? Command { get; }
    internal INamedTypeSymbol? ResultlessCommand { get; }
    internal INamedTypeSymbol? Request { get; }
    internal INamedTypeSymbol? Notification { get; }
    internal INamedTypeSymbol? PipelineBehavior { get; }
    internal INamedTypeSymbol? Unit { get; }

    internal static DispatcherSymbols? Resolve(Compilation compilation)
    {
        var queryHandler = compilation.GetTypeByMetadataName("Dispatcher.IQueryHandler`2");
        var commandHandler = compilation.GetTypeByMetadataName("Dispatcher.ICommandHandler`2");
        var resultlessCommandHandler = compilation.GetTypeByMetadataName("Dispatcher.ICommandHandler`1");
        var notificationHandler = compilation.GetTypeByMetadataName("Dispatcher.INotificationHandler`1");
        if (queryHandler is null || commandHandler is null || resultlessCommandHandler is null ||
            notificationHandler is null)
        {
            return null;
        }

        return new DispatcherSymbols(
            queryHandler,
            commandHandler,
            resultlessCommandHandler,
            notificationHandler,
            compilation.GetTypeByMetadataName("Dispatcher.IQuery`1"),
            compilation.GetTypeByMetadataName("Dispatcher.ICommand`1"),
            compilation.GetTypeByMetadataName("Dispatcher.ICommand"),
            compilation.GetTypeByMetadataName("Dispatcher.IRequest"),
            compilation.GetTypeByMetadataName("Dispatcher.INotification"),
            compilation.GetTypeByMetadataName("Dispatcher.IPipelineBehavior`2"),
            compilation.GetTypeByMetadataName("Dispatcher.Unit"));
    }

    internal bool IsHandler(INamedTypeSymbol definition) =>
        SymbolEqualityComparer.Default.Equals(definition, QueryHandler) ||
        SymbolEqualityComparer.Default.Equals(definition, CommandHandler) ||
        SymbolEqualityComparer.Default.Equals(definition, ResultlessCommandHandler) ||
        SymbolEqualityComparer.Default.Equals(definition, NotificationHandler);
}