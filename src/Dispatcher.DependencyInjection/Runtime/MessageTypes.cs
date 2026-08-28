namespace Dispatcher;

/// <summary>
/// The message type rules shared by handler scanning, route target tracking, and registry creation.
/// </summary>
internal static class MessageTypes
{
    /// <summary>
    /// Determines whether the type is a concrete request or notification, which is what a route may
    /// be created for.
    /// </summary>
    internal static bool IsConcreteMessage(Type type) =>
        IsConcrete(type) && (IsRequest(type) || IsNotification(type));

    internal static bool IsConcreteRequest(Type type) => IsConcrete(type) && IsRequest(type);

    internal static bool IsConcreteNotification(Type type) => IsConcrete(type) && IsNotification(type);

    /// <summary>
    /// Gets the message type itself, its base classes, and its interfaces, which are the types a
    /// handler can declare to handle the message.
    /// </summary>
    internal static IEnumerable<Type> GetAssignableTypes(Type messageType)
    {
        for (var current = messageType; current is not null; current = current.BaseType)
        {
            yield return current;
        }

        foreach (var declaredInterface in messageType.GetInterfaces())
        {
            yield return declaredInterface;
        }
    }

    private static bool IsConcrete(Type type) =>
        type is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false };

    private static bool IsRequest(Type type) => typeof(IRequest).IsAssignableFrom(type);

    private static bool IsNotification(Type type) => typeof(INotification).IsAssignableFrom(type);
}