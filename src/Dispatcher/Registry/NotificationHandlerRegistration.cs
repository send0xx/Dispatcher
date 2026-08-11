namespace Dispatcher;

/// <summary>
/// Describes a notification handler registration.
/// </summary>
public sealed record NotificationHandlerRegistration : HandlerRegistration
{
    /// <summary>
    /// Initializes a notification handler registration.
    /// </summary>
    /// <param name="messageType">The handled notification type.</param>
    /// <param name="handlerType">The concrete handler type.</param>
    public NotificationHandlerRegistration(Type messageType, Type handlerType)
        : base(messageType, handlerType)
    {
    }

    internal NotificationHandlerWrapperFactory? WrapperFactory { get; init; }

    /// <summary>
    /// Creates a notification handler registration.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <typeparam name="THandler">The notification handler implementation type.</typeparam>
    /// <returns>The notification handler registration.</returns>
    public static NotificationHandlerRegistration Create<TNotification, THandler>()
        where TNotification : INotification
        where THandler : class, INotificationHandler<TNotification> =>
        new(typeof(TNotification), typeof(THandler))
        {
            WrapperFactory = new NotificationHandlerWrapperFactory<TNotification>()
        };
}
