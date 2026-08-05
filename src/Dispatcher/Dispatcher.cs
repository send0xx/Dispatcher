namespace Dispatcher;

public sealed class Dispatcher(IServiceProvider serviceProvider, DispatcherRegistry registry) : IDispatcher
{
    private readonly PipelineCache _pipelineCache = new(serviceProvider);

    public ValueTask<TResponse> QueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!registry.RequestHandlers.TryGetValue(query.GetType(), out var wrapper))
        {
            throw new HandlerNotFoundException(query.GetType());
        }

        if (wrapper is not QueryHandlerWrapper<TResponse> typedWrapper)
        {
            throw InvalidMessageShape(query.GetType());
        }

        return typedWrapper.HandleAsync(query, serviceProvider, _pipelineCache, cancellationToken);
    }

    public ValueTask<TResponse> ExecuteAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!registry.RequestHandlers.TryGetValue(command.GetType(), out var wrapper))
        {
            throw new HandlerNotFoundException(command.GetType());
        }

        if (wrapper is not CommandWithResponseHandlerWrapper<TResponse> typedWrapper)
        {
            throw InvalidMessageShape(command.GetType());
        }

        return typedWrapper.HandleAsync(command, serviceProvider, _pipelineCache, cancellationToken);
    }

    public ValueTask ExecuteAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!registry.RequestHandlers.TryGetValue(command.GetType(), out var wrapper))
        {
            throw new HandlerNotFoundException(command.GetType());
        }

        if (wrapper is not CommandHandlerWrapperBase typedWrapper)
        {
            throw InvalidMessageShape(command.GetType());
        }

        return typedWrapper.HandleAsync(command, serviceProvider, _pipelineCache, cancellationToken);
    }

    public ValueTask PublishAsync<TNotification>(
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        return registry.NotificationHandlers.TryGetValue(notification.GetType(), out var wrapper)
            ? wrapper.HandleAsync(notification, serviceProvider, cancellationToken)
            : ValueTask.CompletedTask;
    }

    private static InvalidOperationException InvalidMessageShape(Type messageType) =>
        new($"The registered handler shape does not match message type '{messageType.FullName}'.");
}