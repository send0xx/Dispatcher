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
        var scanState = AssemblyScanState.FindScanState(services);
        var scanned = ScanAssemblies(scanState, assemblies);
        if (scanned.Count == 0)
        {
            return services;
        }

        // Route-target assemblies are loaded before registrations or scan markers are committed. A
        // failure therefore leaves the collection unchanged and allows the caller to retry.
        var additionalRouteTargetScans = ScanAdditionalRouteTargetAssemblies(scanState, scanned);
        var existing = ExistingRegistrations.Read(
            services,
            scanned.SelectMany(static assembly => assembly.Candidates));
        scanState ??= AssemblyScanState.CreateScanState(services);

        var mark = scanState.RouteTargets.MarkPending();
        foreach (var (assembly, types, candidates) in scanned)
        {
            scanState.HandlerAssemblies.Add(assembly);
            RegisterHandlers(services, candidates, lifetime, existing);
            scanState.RouteTargets.Add(assembly, types);
        }

        if (additionalRouteTargetScans is not null)
        {
            foreach (var (assembly, types) in additionalRouteTargetScans)
            {
                scanState.RouteTargets.Add(assembly, types);
            }
        }

        scanState.RouteTargets.Update(mark, existing);

        return services;
    }

    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static List<ScannedAssembly> ScanAssemblies(
        AssemblyScanState? scanState,
        IEnumerable<Assembly> assemblies)
    {
        var scanned = new List<ScannedAssembly>();
        var unsupportedHandlers = new Dictionary<Type, string>();
        foreach (var assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);

            if (scanState?.HandlerAssemblies.Contains(assembly) == true)
            {
                continue;
            }

            var types = GetTypes(assembly);
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

    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static List<RouteTargetScan>? ScanAdditionalRouteTargetAssemblies(
        AssemblyScanState? scanState,
        IReadOnlyList<ScannedAssembly> scanned)
    {
        List<RouteTargetScan>? routeTargetScans = null;
        foreach (var scannedAssembly in scanned)
        {
            foreach (var candidate in scannedAssembly.Candidates)
            {
                if (candidate.IsOpenNotificationHandler)
                {
                    foreach (var constraint in candidate.ImplementationType
                                 .GetGenericArguments()[0]
                                 .GetGenericParameterConstraints())
                    {
                        AddAdditionalRouteTargetAssembly(
                            constraint.Assembly,
                            scanState,
                            scanned,
                            ref routeTargetScans);
                    }
                }
                else
                {
                    AddAdditionalRouteTargetAssembly(
                        candidate.Descriptor.MessageType.Assembly,
                        scanState,
                        scanned,
                        ref routeTargetScans);
                }
            }
        }

        return routeTargetScans;
    }

    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static void AddAdditionalRouteTargetAssembly(
        Assembly assembly,
        AssemblyScanState? scanState,
        IReadOnlyList<ScannedAssembly> scanned,
        ref List<RouteTargetScan>? routeTargetScans)
    {
        if (scanState?.RouteTargets.NeedsScan(assembly) == false)
        {
            return;
        }

        foreach (var scannedAssembly in scanned)
        {
            if (scannedAssembly.Assembly == assembly)
            {
                return;
            }
        }

        if (routeTargetScans is not null)
        {
            foreach (var planned in routeTargetScans)
            {
                if (planned.Assembly == assembly)
                {
                    return;
                }
            }
        }

        routeTargetScans ??= [];
        routeTargetScans.Add(new RouteTargetScan(assembly, GetTypes(assembly)));
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

            existing.RecordHandler(candidate.Descriptor);
        }
    }

    /// <summary>
    /// The types of an assembly, or an error when any of them cannot be read.
    /// </summary>
    /// <remarks>
    /// A type that fails to load cannot be inspected, so scanning cannot tell whether it was a
    /// handler. Continuing with the types that did load would silently drop any handler among them,
    /// which fails invisibly later as a notification that never arrives or a missing request
    /// handler, so the whole scan fails instead.
    /// </remarks>
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

    private readonly record struct ScannedAssembly(
        Assembly Assembly,
        Type[] Types,
        HandlerCandidate[] Candidates);

    private readonly record struct RouteTargetScan(Assembly Assembly, Type[] Types);
}