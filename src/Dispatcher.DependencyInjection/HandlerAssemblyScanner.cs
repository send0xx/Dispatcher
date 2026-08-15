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

        var handlerCandidates = new List<(Type ImplementationType, Type ServiceType, HandlerRegistration Registration)[]>();
        foreach (var (assembly, types, candidates) in scanned)
        {
            scanState.HandlerAssemblies.Add(assembly);
            handlerCandidates.Add(candidates);
            scanState.HasOpenNotificationHandlers |= candidates.Any(static candidate =>
                candidate.Registration is NotificationHandlerRegistration { IsOpenGeneric: true });
            AddMessageScan(scanState, assembly, types);
        }

        var registeredServices = new HashSet<(Type ServiceType, Type ImplementationType)>();
        var registeredHandlers = new HashSet<HandlerRegistration>();
        foreach (var descriptor in services)
        {
            if (descriptor.ImplementationType is { } implementationType)
            {
                registeredServices.Add((descriptor.ServiceType, implementationType));
            }
            else if (descriptor.ImplementationInstance is HandlerRegistration registration)
            {
                registeredHandlers.Add(registration);
            }
        }

        var newlyHandledMessageTypes = new HashSet<Type>();
        foreach (var candidates in handlerCandidates)
        {
            newlyHandledMessageTypes.UnionWith(RegisterHandlers(
                services,
                candidates,
                lifetime,
                registeredServices,
                registeredHandlers));
        }

        var handledMessageTypes = services
            .Where(static descriptor => descriptor.ServiceType == typeof(HandlerRegistration))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<HandlerRegistration>()
            .Where(static registration =>
                registration is not NotificationHandlerRegistration { IsOpenGeneric: true })
            .Select(static registration => registration.MessageType)
            .ToHashSet();
        scanState.HasOpenNotificationHandlers |= services
            .Where(static descriptor => descriptor.ServiceType == typeof(HandlerRegistration))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<NotificationHandlerRegistration>()
            .Any(static registration => registration.IsOpenGeneric);
        var registeredMessageTypes = services
            .Where(static descriptor => descriptor.ServiceType == typeof(MessageRegistration))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<MessageRegistration>()
            .Select(static registration => registration.MessageType)
            .ToHashSet();

        var messageAssemblies = newlyHandledMessageTypes
            .Select(static messageType => messageType.Assembly)
            .Concat(handlerCandidates
                .SelectMany(static candidates => candidates)
                .Where(static candidate =>
                    candidate.Registration is NotificationHandlerRegistration { IsOpenGeneric: true })
                .SelectMany(static candidate => candidate.ImplementationType
                    .GetGenericArguments()[0]
                    .GetGenericParameterConstraints())
                .Select(static constraint => constraint.Assembly));
        foreach (var messageAssembly in messageAssemblies
                     .Distinct())
        {
            if (!scanState.MessageTypes.ContainsKey(messageAssembly))
            {
                AddMessageScan(
                    scanState,
                    messageAssembly,
                    GetLoadableTypes(messageAssembly));
            }
        }

        foreach (var messageTypes in scanState.MessageTypes.Values)
        {
            RegisterMessages(
                services,
                messageTypes,
                handledMessageTypes,
                registeredMessageTypes,
                scanState.HasOpenNotificationHandlers);
        }

        return services;
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
        HashSet<(Type ServiceType, Type ImplementationType)> registeredServices,
        HashSet<HandlerRegistration> registeredHandlers)
    {
        var handledMessageTypes = new HashSet<Type>();
        foreach (var (implementationType, serviceType, registration) in candidates)
        {
            // The typed registration methods may already have registered this handler. Adding it a
            // second time makes notification handlers run twice and makes queries and commands look
            // like they have duplicate handlers.
            if (registeredServices.Add((serviceType, implementationType)))
            {
                services.Add(ServiceDescriptor.Describe(serviceType, implementationType, lifetime));
            }

            if (registeredHandlers.Add(registration))
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
        IEnumerable<Type> types,
        IReadOnlySet<Type> handledMessageTypes,
        ISet<Type> registeredMessageTypes,
        bool hasOpenNotificationHandlers)
    {
        foreach (var messageType in types)
        {
            if (handledMessageTypes.Contains(messageType) ||
                registeredMessageTypes.Contains(messageType) ||
                !HasHandledBaseType(messageType, handledMessageTypes) &&
                !(hasOpenNotificationHandlers && typeof(INotification).IsAssignableFrom(messageType)))
            {
                continue;
            }

            services.AddSingleton(new MessageRegistration(messageType));
            registeredMessageTypes.Add(messageType);
        }
    }

    private static void AddMessageScan(
        AssemblyScanState scanState,
        Assembly assembly,
        IEnumerable<Type> types)
    {
        if (scanState.MessageTypes.ContainsKey(assembly))
        {
            return;
        }

        scanState.MessageTypes.Add(
            assembly,
            types
                .Where(IsConcreteMessage)
                .OrderBy(static type => type.FullName, StringComparer.Ordinal)
                .ToArray());
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
        internal Dictionary<Assembly, Type[]> MessageTypes { get; } = [];
        internal bool HasOpenNotificationHandlers { get; set; }
    }
}