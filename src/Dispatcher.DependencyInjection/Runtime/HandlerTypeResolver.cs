namespace Dispatcher;

internal static class HandlerTypeResolver
{
    internal static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && IsHandlerDefinition(type.GetGenericTypeDefinition());

    internal static bool IsQueryHandlerDefinition(Type type) =>
        type == typeof(IQueryHandler<,>);

    internal static bool IsCommandWithResponseHandlerDefinition(Type type) =>
        type == typeof(ICommandHandler<,>);

    internal static bool IsCommandHandlerDefinition(Type type) =>
        type == typeof(ICommandHandler<>);

    internal static bool IsNotificationHandlerDefinition(Type type) =>
        type == typeof(INotificationHandler<>);

    internal static bool IsOpenNotificationHandler(Type handlerType)
    {
        if (!handlerType.IsGenericTypeDefinition ||
            handlerType.GetGenericArguments() is not [var parameter])
        {
            return false;
        }

        foreach (var handlerInterface in handlerType.GetInterfaces())
        {
            if (handlerInterface.IsGenericType &&
                IsNotificationHandlerDefinition(handlerInterface.GetGenericTypeDefinition()) &&
                handlerInterface.GetGenericArguments()[0] == parameter)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHandlerDefinition(Type type) =>
        IsQueryHandlerDefinition(type) ||
        IsCommandWithResponseHandlerDefinition(type) ||
        IsCommandHandlerDefinition(type) ||
        IsNotificationHandlerDefinition(type);
}