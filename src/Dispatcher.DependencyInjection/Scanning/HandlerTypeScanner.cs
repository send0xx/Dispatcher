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

    private static readonly HashSet<Type> HandlerInterfaces =
    [
        typeof(IQueryHandler<,>),
        typeof(ICommandHandler<,>),
        typeof(ICommandHandler<>),
        typeof(INotificationHandler<>)
    ];

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
                .Where(IsHandlerInterface)
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

            if (type.GetConstructors().Length == 0)
            {
                unsupportedHandlers[type] = MissingPublicConstructor;
                continue;
            }

            candidates.AddRange(serviceTypes.Select(serviceType =>
                new HandlerCandidate(type, serviceType, CreateDescriptor(serviceType, type))));
        }

        return candidates.ToArray();
    }

    private static void AddOpenGenericCandidate(
        Type type,
        Type[] serviceTypes,
        List<HandlerCandidate> candidates,
        Dictionary<Type, string> unsupportedHandlers)
    {
        var typeParameter = type.IsGenericTypeDefinition && type.GetGenericArguments() is [var parameter]
            ? parameter
            : null;
        var handlesItsOwnTypeParameter = typeParameter is not null && serviceTypes.Any(serviceType =>
            serviceType.IsGenericType &&
            serviceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>) &&
            serviceType.GetGenericArguments()[0] == typeParameter);
        if (!handlesItsOwnTypeParameter || serviceTypes.Length != 1)
        {
            unsupportedHandlers[type] = UnsupportedOpenGenericShape;
            return;
        }

        if (type.GetConstructors().Length == 0)
        {
            unsupportedHandlers[type] = MissingPublicConstructor;
            return;
        }

        // An open generic notification handler is registered as itself rather than as
        // INotificationHandler<>, so that closed handler enumeration stays isolated from it.
        candidates.Add(new HandlerCandidate(
            type,
            type,
            new NotificationHandlerDescriptor(typeParameter!, type, true)));
    }

    private static HandlerDescriptor CreateDescriptor(Type serviceType, Type handlerType)
    {
        var definition = serviceType.GetGenericTypeDefinition();
        var arguments = serviceType.GetGenericArguments();

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

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && HandlerInterfaces.Contains(type.GetGenericTypeDefinition());
}