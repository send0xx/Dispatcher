using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

internal static class HandlerAssemblyScanner
{
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
        var handlerCandidates = new List<(Type ImplementationType, Type[] ServiceTypes)[]>();
        foreach (var assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);

            if (scanState.HandlerAssemblies.Contains(assembly))
            {
                continue;
            }

            var types = GetLoadableTypes(assembly);
            scanState.HandlerAssemblies.Add(assembly);
            handlerCandidates.Add(GetHandlerCandidates(types));
            AddMessageScan(scanState, assembly, types);
        }

        if (handlerCandidates.Count == 0)
        {
            return services;
        }

        var newlyHandledMessageTypes = new HashSet<Type>();
        foreach (var candidates in handlerCandidates)
        {
            newlyHandledMessageTypes.UnionWith(RegisterHandlers(services, candidates, lifetime));
        }

        var handledMessageTypes = services
            .Where(static descriptor => descriptor.ServiceType == typeof(HandlerRegistration))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<HandlerRegistration>()
            .Select(static registration => registration.MessageType)
            .ToHashSet();
        var registeredMessageTypes = services
            .Where(static descriptor => descriptor.ServiceType == typeof(MessageRegistration))
            .Select(static descriptor => descriptor.ImplementationInstance)
            .OfType<MessageRegistration>()
            .Select(static registration => registration.MessageType)
            .ToHashSet();

        foreach (var messageAssembly in newlyHandledMessageTypes
                     .Select(static messageType => messageType.Assembly)
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
                registeredMessageTypes);
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
        IEnumerable<(Type ImplementationType, Type[] ServiceTypes)> candidates,
        ServiceLifetime lifetime)
    {
        var handledMessageTypes = new HashSet<Type>();
        foreach (var (implementationType, serviceTypes) in candidates)
        {
            foreach (var serviceType in serviceTypes)
            {
                services.Add(ServiceDescriptor.Describe(serviceType, implementationType, lifetime));
                var registration = CreateRegistration(serviceType, implementationType);
                services.AddSingleton(registration);
                handledMessageTypes.Add(registration.MessageType);
            }
        }

        return handledMessageTypes;
    }

    private static void RegisterMessages(
        IServiceCollection services,
        IEnumerable<Type> types,
        IReadOnlySet<Type> handledMessageTypes,
        ISet<Type> registeredMessageTypes)
    {
        foreach (var messageType in types)
        {
            if (handledMessageTypes.Contains(messageType) ||
                registeredMessageTypes.Contains(messageType) ||
                !HasHandledBaseType(messageType, handledMessageTypes))
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

    private static (Type ImplementationType, Type[] ServiceTypes)[] GetHandlerCandidates(
        IEnumerable<Type> types) =>
        types
            .Where(static type =>
                type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false })
            .Select(type => (
                ImplementationType: type,
                ServiceTypes: type.GetInterfaces()
                    .Where(IsHandlerInterface)
                    .OrderBy(static serviceType => serviceType.FullName, StringComparer.Ordinal)
                    .ToArray()))
            .Where(static candidate => candidate.ServiceTypes.Length > 0)
            .OrderBy(static candidate => candidate.ImplementationType.FullName, StringComparer.Ordinal)
            .ToArray();

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
    }
}