namespace Dispatcher;

internal static class MostSpecificTypeSelector
{
    internal static Type? Select(Type messageType, IEnumerable<Type> candidateTypes)
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
}