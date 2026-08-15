using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// Registers the handlers declared by one or more assemblies, together with the message metadata
/// their routes need. Scanning an assembly twice is a no-op, and registering a handler that another
/// path already registered is too.
/// </summary>
internal static class HandlerAssemblyScanner
{
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    internal static IServiceCollection Register(
        IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ServiceLifetime lifetime)
    {
        var scanState = GetOrCreateScanState(services);
        var scanned = ScanAssemblies(scanState, assemblies);
        if (scanned.Count == 0)
        {
            return services;
        }

        var existing = ExistingRegistrations.Read(
            services,
            scanned.SelectMany(static assembly => assembly.Candidates));
        var hadOpenNotificationHandlers = scanState.HasOpenNotificationHandlers;
        scanState.HasOpenNotificationHandlers |= existing.HasOpenNotificationHandler;

        var mark = scanState.RouteTargets.Mark();
        foreach (var (assembly, types, candidates) in scanned)
        {
            scanState.HandlerAssemblies.Add(assembly);
            scanState.HasOpenNotificationHandlers |= candidates.Any(static candidate =>
                candidate.IsOpenNotificationHandler);
            RegisterHandlers(services, candidates, lifetime, existing);
            scanState.RouteTargets.Add(assembly, types);
        }

        // A handled message often lives in a shared contracts assembly that declares derived types the
        // handler assembly never mentions, so those assemblies are scanned for route targets too.
        foreach (var messageAssembly in MessageAssemblies(scanned).Distinct())
        {
            if (scanState.RouteTargets.NeedsScan(messageAssembly))
            {
                scanState.RouteTargets.Add(messageAssembly, GetLoadableTypes(messageAssembly));
            }
        }

        scanState.RouteTargets.Register(
            services,
            mark,
            existing,
            scanState.HasOpenNotificationHandlers,
            scanState.HasOpenNotificationHandlers != hadOpenNotificationHandlers);

        return services;
    }

    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static List<ScannedAssembly> ScanAssemblies(
        AssemblyScanState scanState,
        IEnumerable<Assembly> assemblies)
    {
        var scanned = new List<ScannedAssembly>();
        var unsupportedHandlers = new Dictionary<Type, string>();
        foreach (var assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);

            if (scanState.HandlerAssemblies.Contains(assembly))
            {
                continue;
            }

            var types = GetLoadableTypes(assembly);
            scanned.Add(new ScannedAssembly(
                assembly,
                types,
                HandlerTypeScanner.Scan(types, unsupportedHandlers)));
        }

        // Every offending handler is reported together, and nothing has been committed to the service
        // collection or the scan state yet, so one bad handler cannot hide the rest or leave a
        // half-scanned assembly behind.
        if (unsupportedHandlers.Count > 0)
        {
            throw new UnsupportedHandlerException(unsupportedHandlers);
        }

        return scanned;
    }

    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static void RegisterHandlers(
        IServiceCollection services,
        IEnumerable<HandlerCandidate> candidates,
        ServiceLifetime lifetime,
        ExistingRegistrations existing)
    {
        foreach (var candidate in candidates)
        {
            if (existing.TryClaimServiceDescriptor(candidate))
            {
                services.Add(ServiceDescriptor.Describe(
                    candidate.ServiceType,
                    candidate.ImplementationType,
                    lifetime));
            }

            if (existing.TryClaimRegistrationMetadata(candidate))
            {
                services.AddSingleton(candidate.Registration);
            }

            if (!candidate.IsOpenNotificationHandler)
            {
                existing.HandledMessageTypes.Add(candidate.Registration.MessageType);
            }
        }
    }

    /// <summary>
    /// The assemblies that may declare route targets for the handlers this scan registered: the ones
    /// declaring each handled message type, and the ones declaring the constraints of each open
    /// generic notification handler.
    /// </summary>
    private static IEnumerable<Assembly> MessageAssemblies(IEnumerable<ScannedAssembly> scanned)
    {
        var candidates = scanned.SelectMany(static assembly => assembly.Candidates).ToArray();

        return candidates
            .Where(static candidate => !candidate.IsOpenNotificationHandler)
            .Select(static candidate => candidate.Registration.MessageType.Assembly)
            .Concat(candidates
                .Where(static candidate => candidate.IsOpenNotificationHandler)
                .SelectMany(static candidate => candidate.ImplementationType
                    .GetGenericArguments()[0]
                    .GetGenericParameterConstraints())
                .Select(static constraint => constraint.Assembly));
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

    private readonly record struct ScannedAssembly(
        Assembly Assembly,
        Type[] Types,
        HandlerCandidate[] Candidates);

    /// <summary>
    /// What one service collection has already been scanned into, kept as a singleton so that later
    /// calls stay idempotent and can route messages their own assemblies did not declare.
    /// </summary>
    private sealed class AssemblyScanState
    {
        internal HashSet<Assembly> HandlerAssemblies { get; } = [];
        internal MessageRouteTargets RouteTargets { get; } = new();
        internal bool HasOpenNotificationHandlers { get; set; }
    }
}