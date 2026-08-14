namespace Dispatcher;

/// <summary>
/// Represents a notification handler registration.
/// </summary>
public sealed record NotificationHandlerRegistration : HandlerRegistration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationHandlerRegistration"/> class.
    /// </summary>
    /// <param name="messageType">
    /// The handled notification type, or the handler type parameter for an open generic registration.
    /// </param>
    /// <param name="handlerType">The closed handler type or open generic handler type definition.</param>
    public NotificationHandlerRegistration(Type messageType, Type handlerType)
        : base(messageType, handlerType)
    {
    }

    /// <summary>
    /// Gets a value indicating whether the handler is an open generic notification handler.
    /// </summary>
    /// <value>
    /// <see langword="true"/> when <see cref="HandlerRegistration.HandlerType"/> is a generic type definition;
    /// otherwise, <see langword="false"/>.
    /// </value>
    public bool IsOpenGeneric => HandlerType.IsGenericTypeDefinition;
}