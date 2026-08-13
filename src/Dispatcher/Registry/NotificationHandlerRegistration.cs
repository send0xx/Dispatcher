namespace Dispatcher;

/// <summary>
/// Represents a notification handler registration.
/// </summary>
public sealed record NotificationHandlerRegistration : HandlerRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationHandlerRegistration"/> class.
    /// </summary>
    /// <param name="messageType">The handled notification type.</param>
    /// <param name="handlerType">The concrete handler type.</param>
    public NotificationHandlerRegistration(Type messageType, Type handlerType)
        : base(messageType, handlerType)
    {
    }

    /// <summary>
    /// Creates a notification handler registration.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to register.</typeparam>
    /// <typeparam name="THandler">The type of notification handler to register.</typeparam>
    /// <returns>A notification handler registration for the specified types.</returns>
    public static NotificationHandlerRegistration Create<TNotification, THandler>()
        where TNotification : INotification
        where THandler : class, INotificationHandler<TNotification> =>
        new(typeof(TNotification), typeof(THandler));
}