namespace Dispatcher;

/// <summary>
/// Maps handler interfaces to the descriptors identifying the message they handle. Assembly scanning
/// and service descriptor reading classify handlers through this one mapping.
/// </summary>
internal static class HandlerDescriptorFactory
{
    private static readonly HashSet<Type> HandlerInterfaces =
    [
        typeof(IQueryHandler<,>),
        typeof(ICommandHandler<,>),
        typeof(ICommandHandler<>),
        typeof(INotificationHandler<>)
    ];

    internal static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && HandlerInterfaces.Contains(type.GetGenericTypeDefinition());

    /// <param name="handlerInterface">
    /// A handler interface implemented by <paramref name="handlerType"/>, as accepted by
    /// <see cref="IsHandlerInterface"/>.
    /// </param>
    /// <param name="handlerType">The handler implementation type.</param>
    internal static HandlerDescriptor Create(Type handlerInterface, Type handlerType)
    {
        var definition = handlerInterface.GetGenericTypeDefinition();
        var arguments = handlerInterface.GetGenericArguments();

        if (definition == typeof(IQueryHandler<,>))
        {
            return new QueryHandlerDescriptor(arguments[0], arguments[1], handlerType);
        }

        if (definition == typeof(ICommandHandler<,>))
        {
            return new CommandWithResponseHandlerDescriptor(arguments[0], arguments[1], handlerType);
        }

        if (definition == typeof(ICommandHandler<>))
        {
            return new CommandHandlerDescriptor(arguments[0], handlerType);
        }

        return new NotificationHandlerDescriptor(arguments[0], handlerType, false);
    }

    /// <summary>
    /// Creates the descriptor of an open generic notification handler, or returns
    /// <see langword="null"/> when the type is not one.
    /// </summary>
    /// <remarks>
    /// A supported handler is a generic type definition with one type parameter that it handles
    /// through <see cref="INotificationHandler{TNotification}"/> using that parameter directly.
    /// </remarks>
    internal static NotificationHandlerDescriptor? CreateOpenNotification(Type handlerType) =>
        handlerType.IsGenericTypeDefinition &&
        handlerType.GetGenericArguments() is [var parameter] &&
        HandlesNotificationParameter(handlerType, parameter)
            ? new NotificationHandlerDescriptor(parameter, handlerType, true)
            : null;

    private static bool HandlesNotificationParameter(Type handlerType, Type parameter) =>
        handlerType.GetInterfaces().Any(handlerInterface =>
            handlerInterface.IsGenericType &&
            handlerInterface.GetGenericTypeDefinition() == typeof(INotificationHandler<>) &&
            handlerInterface.GetGenericArguments()[0] == parameter);
}