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
        List<Type> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        Type? selected = null;
        List<Type>? ambiguous = null;
        for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            var isMostSpecific = true;
            for (var otherIndex = 0; otherIndex < candidates.Count; otherIndex++)
            {
                var other = candidates[otherIndex];
                if (candidate != other && candidate.IsAssignableFrom(other))
                {
                    isMostSpecific = false;
                    break;
                }
            }

            if (!isMostSpecific)
            {
                continue;
            }

            if (selected is null)
            {
                selected = candidate;
                continue;
            }

            ambiguous ??= [selected];
            ambiguous.Add(candidate);
        }

        return ambiguous is null
            ? selected
            : throw new AmbiguousHandlerException(messageType, ambiguous);
    }

    private static bool IsConcrete(Type type) =>
        type is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false };

    private static bool IsRequest(Type type) => typeof(IRequest).IsAssignableFrom(type);

    private static bool IsNotification(Type type) => typeof(INotification).IsAssignableFrom(type);
}