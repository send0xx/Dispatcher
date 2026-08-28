namespace Dispatcher.DependencyInjection;

/// <summary>
/// Classifies the types of a scanned assembly into registrable handler candidates, recording the
/// reason for every handler that cannot be registered instead of skipping it.
/// </summary>
internal static class HandlerTypeScanner
{
    private const string UnsupportedOpenGenericShape =
        "is generic but is not a supported open generic handler. Use a closed handler type, or an " +
        "open generic notification handler with one type parameter that implements " +
        "INotificationHandler<TNotification> using that parameter directly.";
    private const string MissingPublicConstructor = "must expose a public constructor.";

    /// <param name="types">The types of the assembly being scanned.</param>
    /// <param name="unsupportedHandlers">
    /// Collects each handler that cannot be registered, mapped to the reason. The caller reports them
    /// together, so scanning continues past the first offending type.
    /// </param>
    internal static HandlerCandidate[] Scan(
        IEnumerable<Type> types,
        Dictionary<Type, string> unsupportedHandlers)
    {
        var candidates = new List<HandlerCandidate>();
        foreach (var type in types
                     .Where(static type => type is { IsClass: true, IsAbstract: false })
                     .OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            var serviceTypes = type.GetInterfaces()
                .Where(HandlerDescriptorFactory.IsHandlerInterface)
                .OrderBy(static serviceType => serviceType.FullName, StringComparer.Ordinal)
                .ToArray();
            if (serviceTypes.Length == 0)
            {
                continue;
            }

            if (type.ContainsGenericParameters)
            {
                AddOpenGenericCandidate(type, serviceTypes, candidates, unsupportedHandlers);
                continue;
            }

            if (!HasPublicConstructor(type, unsupportedHandlers))
            {
                continue;
            }

            candidates.AddRange(serviceTypes.Select(serviceType => new HandlerCandidate(
                type,
                serviceType,
                HandlerDescriptorFactory.Create(serviceType, type))));
        }

        return candidates.ToArray();
    }

    private static void AddOpenGenericCandidate(
        Type type,
        Type[] serviceTypes,
        List<HandlerCandidate> candidates,
        Dictionary<Type, string> unsupportedHandlers)
    {
        // Handling one other notification alongside its own type parameter would leave the handler
        // registered twice for that notification, so only the single-interface shape is supported.
        if (serviceTypes.Length != 1 ||
            HandlerDescriptorFactory.CreateOpenNotification(type) is not { } descriptor)
        {
            unsupportedHandlers[type] = UnsupportedOpenGenericShape;
            return;
        }

        if (!HasPublicConstructor(type, unsupportedHandlers))
        {
            return;
        }

        // An open generic notification handler is registered as itself rather than as
        // INotificationHandler<>, so that closed handler enumeration stays isolated from it.
        candidates.Add(new HandlerCandidate(type, type, descriptor));
    }

    private static bool HasPublicConstructor(Type type, Dictionary<Type, string> unsupportedHandlers)
    {
        if (type.GetConstructors().Length > 0)
        {
            return true;
        }

        unsupportedHandlers[type] = MissingPublicConstructor;
        return false;
    }
}