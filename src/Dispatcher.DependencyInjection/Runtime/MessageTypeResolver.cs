namespace Dispatcher;

internal static class MessageTypeResolver
{
    internal static bool IsConcreteMessage(Type type) =>
        IsConcrete(type) && (IsRequest(type) || IsNotification(type));

    internal static bool IsConcreteRequest(Type type) => IsConcrete(type) && IsRequest(type);

    internal static bool IsConcreteNotification(Type type) => IsConcrete(type) && IsNotification(type);

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

    internal static Type? SelectMostSpecific(
        Type messageType,
        IEnumerable<Type> candidateTypes)
    {
        var candidates = candidateTypes
            .Where(candidate => candidate.IsAssignableFrom(messageType))
            .Distinct()
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var mostSpecific = candidates
            .Where(candidate => !candidates.Any(other =>
                candidate != other && candidate.IsAssignableFrom(other)))
            .ToArray();
        return mostSpecific.Length switch
        {
            1 => mostSpecific[0],
            _ => throw new AmbiguousHandlerException(messageType, mostSpecific)
        };
    }

    private static bool IsConcrete(Type type) =>
        type is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false };

    private static bool IsRequest(Type type) => typeof(IRequest).IsAssignableFrom(type);

    private static bool IsNotification(Type type) => typeof(INotification).IsAssignableFrom(type);
}