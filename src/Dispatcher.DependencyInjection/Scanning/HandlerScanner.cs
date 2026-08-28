using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

internal static class HandlerScanner
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
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        var state = DispatcherRegistrationState.Find(services);
        var scannedHandlers = ScanHandlerAssemblies(state, assemblies, out var candidates);
        if (scannedHandlers.Count == 0)
        {
            return services;
        }

        var scannedMessages = ScanMessageAssemblies(state, scannedHandlers, candidates);
        RegisterHandlers(services, candidates, lifetime);
        RecordScan(
            DispatcherRegistrationState.GetOrCreate(services),
            scannedHandlers,
            scannedMessages);

        return services;
    }

    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static List<ScannedAssembly> ScanHandlerAssemblies(
        DispatcherRegistrationState? state,
        IEnumerable<Assembly> assemblies,
        out List<HandlerCandidate> candidates)
    {
        var scanned = new List<ScannedAssembly>();
        candidates = [];
        var unsupported = new Dictionary<Type, string>();

        foreach (var assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);
            if (state?.HandlerAssemblies.Contains(assembly) == true)
            {
                continue;
            }

            var types = GetTypes(assembly);
            scanned.Add(new ScannedAssembly(assembly, types));
            ScanHandlers(types, candidates, unsupported);
        }

        if (unsupported.Count > 0)
        {
            throw new UnsupportedHandlerException(unsupported);
        }

        return scanned;
    }

    private static void ScanHandlers(
        IEnumerable<Type> types,
        List<HandlerCandidate> candidates,
        Dictionary<Type, string> unsupported)
    {
        foreach (var type in types
                     .Where(static type => type is { IsClass: true, IsAbstract: false })
                     .OrderBy(static type => type.FullName, StringComparer.Ordinal))
        {
            var serviceTypes = type.GetInterfaces()
                .Where(IsHandlerInterface)
                .OrderBy(static type => type.FullName, StringComparer.Ordinal)
                .ToArray();
            if (serviceTypes.Length == 0)
            {
                continue;
            }

            if (type.ContainsGenericParameters)
            {
                AddOpenNotificationHandler(type, serviceTypes, candidates, unsupported);
            }
            else if (HasPublicConstructor(type, unsupported))
            {
                candidates.AddRange(serviceTypes.Select(serviceType =>
                    new HandlerCandidate(serviceType, type, false)));
            }
        }
    }

    private static void AddOpenNotificationHandler(
        Type handlerType,
        Type[] serviceTypes,
        List<HandlerCandidate> candidates,
        Dictionary<Type, string> unsupported)
    {
        if (!handlerType.IsGenericTypeDefinition ||
            handlerType.GetGenericArguments() is not [var parameter] ||
            serviceTypes is not [var serviceType] ||
            serviceType.GetGenericTypeDefinition() != typeof(INotificationHandler<>) ||
            serviceType.GetGenericArguments()[0] != parameter)
        {
            unsupported[handlerType] = UnsupportedOpenGenericShape;
            return;
        }

        if (HasPublicConstructor(handlerType, unsupported))
        {
            candidates.Add(new HandlerCandidate(handlerType, handlerType, true));
        }
    }

    private static bool HasPublicConstructor(
        Type handlerType,
        Dictionary<Type, string> unsupported)
    {
        if (handlerType.GetConstructors().Length > 0)
        {
            return true;
        }

        unsupported[handlerType] = MissingPublicConstructor;
        return false;
    }

    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static List<ScannedAssembly> ScanMessageAssemblies(
        DispatcherRegistrationState? state,
        IReadOnlyList<ScannedAssembly> handlerAssemblies,
        IReadOnlyList<HandlerCandidate> candidates)
    {
        var handlerTypes = handlerAssemblies.ToDictionary(
            static scanned => scanned.Assembly,
            static scanned => scanned.Types);
        var assemblies = handlerAssemblies
            .Select(static scanned => scanned.Assembly)
            .Concat(candidates.SelectMany(static candidate => candidate.GetMessageAssemblies()))
            .Distinct();
        var scanned = new List<ScannedAssembly>();

        foreach (var assembly in assemblies)
        {
            if (state?.MessageAssemblies.Contains(assembly) == true)
            {
                continue;
            }

            var types = handlerTypes.TryGetValue(assembly, out var knownTypes)
                ? knownTypes
                : GetTypes(assembly);
            scanned.Add(new ScannedAssembly(assembly, types));
        }

        return scanned;
    }

    private static void RegisterHandlers(
        IServiceCollection services,
        IEnumerable<HandlerCandidate> candidates,
        ServiceLifetime lifetime)
    {
        var registrations = services
            .Where(static descriptor => !descriptor.IsKeyedService)
            .Select(static descriptor => (
                descriptor.ServiceType,
                ImplementationType: descriptor.ImplementationType ??
                                    descriptor.ImplementationInstance?.GetType()))
            .Where(static registration => registration.ImplementationType is not null)
            .Select(static registration => (
                registration.ServiceType,
                ImplementationType: registration.ImplementationType!))
            .ToHashSet();

        foreach (var candidate in candidates)
        {
            if (registrations.Add((candidate.ServiceType, candidate.ImplementationType)))
            {
                services.Add(ServiceDescriptor.Describe(
                    candidate.ServiceType,
                    candidate.ImplementationType,
                    lifetime));
            }
        }
    }

    private static void RecordScan(
        DispatcherRegistrationState state,
        IEnumerable<ScannedAssembly> handlerAssemblies,
        IEnumerable<ScannedAssembly> messageAssemblies)
    {
        foreach (var scanned in handlerAssemblies)
        {
            state.HandlerAssemblies.Add(scanned.Assembly);
        }

        foreach (var scanned in messageAssemblies)
        {
            state.MessageAssemblies.Add(scanned.Assembly);
            foreach (var messageType in scanned.Types
                         .Where(MessageTypeResolver.IsConcreteMessage)
                         .OrderBy(static type => type.FullName, StringComparer.Ordinal))
            {
                state.MessageTypes.Add(messageType);
            }
        }
    }

    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static Type[] GetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            throw new AssemblyScanException(assembly, exception);
        }
    }

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && HandlerInterfaces.Contains(type.GetGenericTypeDefinition());

    private readonly record struct ScannedAssembly(Assembly Assembly, Type[] Types);

    private readonly record struct HandlerCandidate(
        Type ServiceType,
        Type ImplementationType,
        bool IsOpenNotification)
    {
        internal IEnumerable<Assembly> GetMessageAssemblies()
        {
            if (!IsOpenNotification)
            {
                yield return ServiceType.GetGenericArguments()[0].Assembly;
                yield break;
            }

            foreach (var constraint in ImplementationType
                         .GetGenericArguments()[0]
                         .GetGenericParameterConstraints())
            {
                yield return constraint.Assembly;
            }
        }
    }
}