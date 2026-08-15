using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

internal static class HandlerAssemblyScanner
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

    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    internal static IServiceCollection Register(
        IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ServiceLifetime lifetime)
    {
        var scanState = GetOrCreateScanState(services);
        var scanned = new List<(Assembly Assembly, Type[] Types,
            (Type ImplementationType, Type ServiceType, HandlerRegistration Registration)[] Candidates)>();
        var unsupportedHandlers = new Dictionary<Type, string>();
        foreach (var assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);

            if (scanState.HandlerAssemblies.Contains(assembly))
            {
                continue;
            }

            var types = GetLoadableTypes(assembly);
            scanned.Add((assembly, types, GetHandlerCandidates(types, unsupportedHandlers)));
        }

        // Every offending handler is reported together, and nothing is committed to the service
        // collection or the scan state, so one bad handler cannot hide the rest or leave a
        // half-scanned assembly behind.
        if (unsupportedHandlers.Count > 0)
        {
            throw new UnsupportedHandlerException(unsupportedHandlers);
        }

        if (scanned.Count == 0)
        {
            return services;
        }

        // What this scan may still need to add. Reading the service collection drops whatever another
        // registration path already added, so these stay sized by the scanned handlers instead of by
        // the whole service collection.
        var unregisteredServices = new HashSet<(Type ServiceType, Type ImplementationType)>();
        var unregisteredHandlers = new HashSet<HandlerRegistration>();
        foreach (var (implementationType, serviceType, registration) in
                 scanned.SelectMany(static entry => entry.Candidates))
        {
            unregisteredServices.Add((serviceType, implementationType));
            unregisteredHandlers.Add(registration);
        }

        var previousHasOpenNotificationHandlers = scanState.HasOpenNotificationHandlers;
        var handledMessageTypes = new HashSet<Type>();
        var registeredMessageTypes = new HashSet<Type>();
        ReadRegistrations(
            services,
            scanState,
            handledMessageTypes,
            registeredMessageTypes,
            unregisteredServices,
            unregisteredHandlers);

        var firstNewMessageTypeIndex = scanState.PendingMessageTypes.Count;
        var newlyHandledMessageTypes = new HashSet<Type>();
        foreach (var (assembly, types, candidates) in scanned)
        {
            scanState.HandlerAssemblies.Add(assembly);
            scanState.HasOpenNotificationHandlers |= candidates.Any(static candidate =>
                candidate.Registration is NotificationHandlerRegistration { IsOpenGeneric: true });
            newlyHandledMessageTypes.UnionWith(RegisterHandlers(
                services,
                candidates,
                lifetime,
                unregisteredServices,
                unregisteredHandlers));
            AddMessageScan(scanState, assembly, types);
        }

        handledMessageTypes.UnionWith(newlyHandledMessageTypes);

        var messageAssemblies = newlyHandledMessageTypes
            .Select(static messageType => messageType.Assembly)
            .Concat(scanned
                .SelectMany(static entry => entry.Candidates)
                .Where(static candidate =>
                    candidate.Registration is NotificationHandlerRegistration { IsOpenGeneric: true })
                .SelectMany(static candidate => candidate.ImplementationType
                    .GetGenericArguments()[0]
                    .GetGenericParameterConstraints())
                .Select(static constraint => constraint.Assembly));
        foreach (var messageAssembly in messageAssemblies
                     .Distinct())
        {
            if (!scanState.MessageAssemblies.Contains(messageAssembly))
            {
                AddMessageScan(
                    scanState,
                    messageAssembly,
                    GetLoadableTypes(messageAssembly));
            }
        }

        // A message type that no handler routes stays pending. Whether it can be routed depends only
        // on the handled types and on open notification handlers, so when neither changed this scan
        // only has to consider the message types it just added.
        var routingChanged = scanState.LastHandledMessageTypes is null ||
            scanState.HasOpenNotificationHandlers != previousHasOpenNotificationHandlers ||
            !handledMessageTypes.SetEquals(scanState.LastHandledMessageTypes);
        RegisterMessages(
            services,
            scanState.PendingMessageTypes,
            routingChanged ? 0 : firstNewMessageTypeIndex,
            handledMessageTypes,
            registeredMessageTypes,
            scanState.HasOpenNotificationHandlers);
        scanState.LastHandledMessageTypes = handledMessageTypes;

        return services;
    }

    private static void ReadRegistrations(
        IServiceCollection services,
        AssemblyScanState scanState,
        HashSet<Type> handledMessageTypes,
        HashSet<Type> registeredMessageTypes,
        HashSet<(Type ServiceType, Type ImplementationType)> unregisteredServices,
        HashSet<HandlerRegistration> unregisteredHandlers)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(HandlerRegistration) &&
                descriptor.ImplementationInstance is HandlerRegistration handlerRegistration)
            {
                if (handlerRegistration is NotificationHandlerRegistration { IsOpenGeneric: true })
                {
                    scanState.HasOpenNotificationHandlers = true;
                }
                else
                {
                    handledMessageTypes.Add(handlerRegistration.MessageType);
                }

                unregisteredHandlers.Remove(handlerRegistration);
                continue;
            }

            if (descriptor.ServiceType == typeof(MessageRegistration) &&
                descriptor.ImplementationInstance is MessageRegistration messageRegistration)
            {
                registeredMessageTypes.Add(messageRegistration.MessageType);
                continue;
            }

            if (descriptor.ImplementationType is { } implementationType)
            {
                unregisteredServices.Remove((descriptor.ServiceType, implementationType));
            }
        }
    }

    private static AssemblyScanState GetOrCreateScanState(IServiceCollection services)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(AssemblyScanState) &&
                descriptor.ImplementationInstance is AssemblyScanState state)
            {
                return state;
            }
        }

        var created = new AssemblyScanState();
        services.AddSingleton(created);
        return created;
    }

    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static HashSet<Type> RegisterHandlers(
        IServiceCollection services,
        IEnumerable<(Type ImplementationType, Type ServiceType, HandlerRegistration Registration)> candidates,
        ServiceLifetime lifetime,
        HashSet<(Type ServiceType, Type ImplementationType)> unregisteredServices,
        HashSet<HandlerRegistration> unregisteredHandlers)
    {
        var handledMessageTypes = new HashSet<Type>();
        foreach (var (implementationType, serviceType, registration) in candidates)
        {
            // The typed registration methods may already have registered this handler. Adding it a
            // second time makes notification handlers run twice and makes queries and commands look
            // like they have duplicate handlers.
            if (unregisteredServices.Remove((serviceType, implementationType)))
            {
                services.Add(ServiceDescriptor.Describe(serviceType, implementationType, lifetime));
            }

            if (unregisteredHandlers.Remove(registration))
            {
                services.AddSingleton(registration);
            }

            if (registration is not NotificationHandlerRegistration { IsOpenGeneric: true })
            {
                handledMessageTypes.Add(registration.MessageType);
            }
        }

        return handledMessageTypes;
    }

    private static void RegisterMessages(
        IServiceCollection services,
        List<Type> pendingMessageTypes,
        int startIndex,
        IReadOnlySet<Type> handledMessageTypes,
        ISet<Type> registeredMessageTypes,
        bool hasOpenNotificationHandlers)
    {
        // Types that are registered here, or that another path already covers, are compacted out of
        // the pending list so that later scans never look at them again.
        var remaining = startIndex;
        for (var index = startIndex; index < pendingMessageTypes.Count; index++)
        {
            var messageType = pendingMessageTypes[index];
            if (handledMessageTypes.Contains(messageType) ||
                registeredMessageTypes.Contains(messageType))
            {
                continue;
            }

            if (!HasHandledBaseType(messageType, handledMessageTypes) &&
                !(hasOpenNotificationHandlers && typeof(INotification).IsAssignableFrom(messageType)))
            {
                pendingMessageTypes[remaining++] = messageType;
                continue;
            }

            services.AddSingleton(new MessageRegistration(messageType));
            registeredMessageTypes.Add(messageType);
        }

        pendingMessageTypes.RemoveRange(remaining, pendingMessageTypes.Count - remaining);
    }

    private static void AddMessageScan(
        AssemblyScanState scanState,
        Assembly assembly,
        IEnumerable<Type> types)
    {
        if (!scanState.MessageAssemblies.Add(assembly))
        {
            return;
        }

        scanState.PendingMessageTypes.AddRange(types
            .Where(IsConcreteMessage)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal));
    }

    private static (Type ImplementationType, Type ServiceType, HandlerRegistration Registration)[]
        GetHandlerCandidates(IEnumerable<Type> types, Dictionary<Type, string> unsupportedHandlers)
    {
        var candidates = new List<(Type, Type, HandlerRegistration)>();
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

            if (!type.ContainsGenericParameters)
            {
                if (type.GetConstructors().Length == 0)
                {
                    unsupportedHandlers[type] = MissingPublicConstructor;
                    continue;
                }

                candidates.AddRange(serviceTypes.Select(serviceType => (
                    type,
                    serviceType,
                    CreateRegistration(serviceType, type))));
                continue;
            }

            var typeParameter = type.IsGenericTypeDefinition && type.GetGenericArguments() is [var parameter]
                ? parameter
                : null;
            var notificationService = typeParameter is null
                ? null
                : serviceTypes.FirstOrDefault(serviceType =>
                    serviceType.IsGenericType &&
                    serviceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>) &&
                    serviceType.GetGenericArguments()[0] == typeParameter);
            if (notificationService is null || serviceTypes.Length != 1)
            {
                unsupportedHandlers[type] = UnsupportedOpenGenericShape;
                continue;
            }

            if (type.GetConstructors().Length == 0)
            {
                unsupportedHandlers[type] = MissingPublicConstructor;
                continue;
            }

            candidates.Add((
                type,
                type,
                new NotificationHandlerRegistration(typeParameter!, type)));
        }

        return candidates.ToArray();
    }

    private static bool IsConcreteMessage(Type type) =>
        type is { IsAbstract: false, IsInterface: false, ContainsGenericParameters: false } &&
        (typeof(IRequest).IsAssignableFrom(type) ||
         typeof(INotification).IsAssignableFrom(type));

    private static bool HasHandledBaseType(Type messageType, IReadOnlySet<Type> handledMessageTypes)
    {
        for (var current = messageType.BaseType; current is not null; current = current.BaseType)
        {
            if (handledMessageTypes.Contains(current))
            {
                return true;
            }
        }

        return messageType.GetInterfaces().Any(handledMessageTypes.Contains);
    }

    private static HandlerRegistration CreateRegistration(Type serviceType, Type handlerType)
    {
        var definition = serviceType.GetGenericTypeDefinition();
        var arguments = serviceType.GetGenericArguments();

        if (definition == typeof(IQueryHandler<,>))
        {
            return new QueryHandlerRegistration(arguments[0], arguments[1], handlerType);
        }

        if (definition == typeof(ICommandHandler<,>))
        {
            return new CommandWithResponseHandlerRegistration(arguments[0], arguments[1], handlerType);
        }

        if (definition == typeof(ICommandHandler<>))
        {
            return new CommandHandlerRegistration(arguments[0], handlerType);
        }

        return new NotificationHandlerRegistration(arguments[0], handlerType);
    }

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && HandlerInterfaces.Contains(type.GetGenericTypeDefinition());

    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static Type[] GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>().ToArray();
        }
    }

    private sealed class AssemblyScanState
    {
        internal HashSet<Assembly> HandlerAssemblies { get; } = [];
        internal HashSet<Assembly> MessageAssemblies { get; } = [];

        /// <summary>
        /// Concrete message types that no scan has been able to route yet, in registration order.
        /// </summary>
        internal List<Type> PendingMessageTypes { get; } = [];

        internal bool HasOpenNotificationHandlers { get; set; }

        /// <summary>
        /// The handled message types the pending list was last fully reconsidered against, or
        /// <see langword="null"/> before the first scan. Comparing the set rather than its size keeps
        /// a message routable even if a handler is removed and another added between scans.
        /// </summary>
        internal HashSet<Type>? LastHandledMessageTypes { get; set; }
    }
}