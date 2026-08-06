namespace Dispatcher;

/// <summary>
/// Describes a handler discovered during application registration.
/// </summary>
/// <param name="MessageType">The handled message type.</param>
/// <param name="ResponseType">The response type, or <see langword="null"/> when the handler has no response.</param>
/// <param name="Kind">The handler kind.</param>
/// <param name="HandlerType">The concrete handler type.</param>
public sealed record HandlerRegistration(
    Type MessageType,
    Type? ResponseType,
    HandlerKind Kind,
    Type HandlerType)
{
    internal RequestHandlerWrapper? RequestWrapper { get; init; }
    internal NotificationHandlerWrapper? NotificationWrapper { get; init; }

    /// <summary>
    /// Creates an AOT-safe query handler registration with a closed dispatch wrapper.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The query handler implementation type.</typeparam>
    /// <returns>The prepared handler registration.</returns>
    public static HandlerRegistration CreateQuery<TQuery, TResponse, THandler>()
        where TQuery : IQuery<TResponse>
        where THandler : class, IQueryHandler<TQuery, TResponse> =>
        new(typeof(TQuery), typeof(TResponse), HandlerKind.Query, typeof(THandler))
        {
            RequestWrapper = new QueryHandlerWrapper<TQuery, TResponse>()
        };

    /// <summary>
    /// Creates an AOT-safe result-bearing command handler registration with a closed dispatch wrapper.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The command handler implementation type.</typeparam>
    /// <returns>The prepared handler registration.</returns>
    public static HandlerRegistration CreateCommand<TCommand, TResponse, THandler>()
        where TCommand : ICommand<TResponse>
        where THandler : class, ICommandHandler<TCommand, TResponse> =>
        new(typeof(TCommand), typeof(TResponse), HandlerKind.CommandWithResponse, typeof(THandler))
        {
            RequestWrapper = new CommandWithResponseHandlerWrapper<TCommand, TResponse>()
        };

    /// <summary>
    /// Creates an AOT-safe resultless command handler registration with a closed dispatch wrapper.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="THandler">The command handler implementation type.</typeparam>
    /// <returns>The prepared handler registration.</returns>
    public static HandlerRegistration CreateCommand<TCommand, THandler>()
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand> =>
        new(typeof(TCommand), null, HandlerKind.Command, typeof(THandler))
        {
            RequestWrapper = new CommandHandlerWrapper<TCommand>()
        };

    /// <summary>
    /// Creates an AOT-safe notification handler registration with a closed dispatch wrapper.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <typeparam name="THandler">The notification handler implementation type.</typeparam>
    /// <returns>The prepared handler registration.</returns>
    public static HandlerRegistration CreateNotification<TNotification, THandler>()
        where TNotification : INotification
        where THandler : class, INotificationHandler<TNotification> =>
        new(typeof(TNotification), null, HandlerKind.Notification, typeof(THandler))
        {
            NotificationWrapper = new NotificationHandlerWrapper<TNotification>()
        };
}