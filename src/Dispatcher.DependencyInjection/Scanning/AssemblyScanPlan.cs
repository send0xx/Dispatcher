using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// What one scan contributes: the handler assemblies it discovered handlers in, and the further
/// assemblies whose message types those handlers can route.
/// </summary>
/// <remarks>
/// Every assembly is read before the plan is applied, so a type load failure or an unsupported
/// handler leaves the service collection and the scan state unchanged and the caller can retry.
/// </remarks>
internal sealed class AssemblyScanPlan
{
    private readonly List<ScannedAssembly> _handlerAssemblies;
    private readonly List<ScannedAssembly> _routeTargetAssemblies;

    private AssemblyScanPlan(
        List<ScannedAssembly> handlerAssemblies,
        List<ScannedAssembly> routeTargetAssemblies)
    {
        _handlerAssemblies = handlerAssemblies;
        _routeTargetAssemblies = routeTargetAssemblies;
    }

    /// <summary>
    /// Gets whether every requested assembly was scanned before, leaving nothing to register.
    /// </summary>
    internal bool IsEmpty => _handlerAssemblies.Count == 0;

    /// <summary>
    /// Gets the handler candidates of every scanned assembly, in scan order.
    /// </summary>
    internal IEnumerable<HandlerCandidate> Candidates =>
        _handlerAssemblies.SelectMany(static scanned => scanned.Candidates);

    /// <param name="scanState">
    /// The scan state of the service collection, or <see langword="null"/> when nothing was scanned
    /// into it yet. Assemblies it already covers are left out of the plan.
    /// </param>
    /// <param name="assemblies">The assemblies to scan for handlers.</param>
    /// <exception cref="AssemblyScanException">An assembly has types that cannot be loaded.</exception>
    /// <exception cref="UnsupportedHandlerException">An assembly declares a handler that cannot be registered.</exception>
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    internal static AssemblyScanPlan Create(
        AssemblyScanState? scanState,
        IEnumerable<Assembly> assemblies)
    {
        var handlerAssemblies = ScanHandlerAssemblies(scanState, assemblies);
        return new AssemblyScanPlan(
            handlerAssemblies,
            handlerAssemblies.Count == 0
                ? []
                : ScanRouteTargetAssemblies(scanState, handlerAssemblies));
    }

    /// <summary>
    /// Records the scanned assemblies and their route targets in the scan state.
    /// </summary>
    /// <param name="scanState">The scan state of the service collection registered into.</param>
    /// <param name="handled">The messages handled once this scan registered its handlers.</param>
    internal void Record(AssemblyScanState scanState, HandledMessages handled)
    {
        var routeTargets = scanState.RouteTargets;
        var mark = routeTargets.MarkPending();
        foreach (var scanned in _handlerAssemblies)
        {
            scanState.HandlerAssemblies.Add(scanned.Assembly);
            routeTargets.Add(scanned.Assembly, scanned.Types);
        }

        foreach (var scanned in _routeTargetAssemblies)
        {
            routeTargets.Add(scanned.Assembly, scanned.Types);
        }

        routeTargets.Update(mark, handled);
    }

    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static List<ScannedAssembly> ScanHandlerAssemblies(
        AssemblyScanState? scanState,
        IEnumerable<Assembly> assemblies)
    {
        var scannedAssemblies = new List<ScannedAssembly>();
        var unsupportedHandlers = new Dictionary<Type, string>();
        foreach (var assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);

            if (scanState?.HandlerAssemblies.Contains(assembly) == true)
            {
                continue;
            }

            var types = GetTypes(assembly);
            scannedAssemblies.Add(new ScannedAssembly(
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

        return scannedAssemblies;
    }

    /// <summary>
    /// Scans the assemblies declaring the handled messages, which contribute route targets only.
    /// </summary>
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static List<ScannedAssembly> ScanRouteTargetAssemblies(
        AssemblyScanState? scanState,
        List<ScannedAssembly> handlerAssemblies)
    {
        var known = new HashSet<Assembly>(handlerAssemblies.Select(static scanned => scanned.Assembly));
        var routeTargetAssemblies = new List<ScannedAssembly>();
        foreach (var assembly in handlerAssemblies
                     .SelectMany(static scanned => scanned.Candidates)
                     .SelectMany(static candidate => candidate.GetMessageAssemblies()))
        {
            if (scanState?.RouteTargets.NeedsScan(assembly) == false || !known.Add(assembly))
            {
                continue;
            }

            routeTargetAssemblies.Add(new ScannedAssembly(assembly, GetTypes(assembly), []));
        }

        return routeTargetAssemblies;
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
}