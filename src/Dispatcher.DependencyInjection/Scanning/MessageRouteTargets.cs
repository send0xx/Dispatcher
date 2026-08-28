using System.Reflection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// Tracks concrete route targets discovered by assembly scanning.
/// </summary>
/// <remarks>
/// Routable targets are retained directly instead of becoming individual service descriptors.
/// Unroutable targets remain pending because a handler registered later may make them routable.
/// </remarks>
internal sealed class MessageRouteTargets
{
    private readonly HashSet<Assembly> _scannedAssemblies = [];
    private readonly List<Type> _pending = [];
    private readonly List<Type> _routable = [];
    private HandledMessages? _lastHandled;

    internal bool NeedsScan(Assembly assembly) => !_scannedAssemblies.Contains(assembly);

    /// <summary>
    /// Gets the known routable targets and any pending targets that final registrations may have made
    /// routable since the last scan.
    /// </summary>
    /// <param name="handlers">Every handler descriptor the registry is being created from.</param>
    /// <remarks>
    /// Registration methods called after the last scan can make a pending message routable, and no
    /// scan runs afterwards to notice. Registry creation therefore reconsiders these against the
    /// final registrations, which is what keeps routing independent of registration order.
    /// Reconsidering a message never asserts that it routes: route creation drops it exactly as
    /// before when it still does not.
    /// </remarks>
    internal IEnumerable<Type> GetRouteTargets(IEnumerable<HandlerDescriptor> handlers)
    {
        foreach (var messageType in _routable)
        {
            yield return messageType;
        }

        if (_pending.Count == 0 || _lastHandled is null || HandledMessages.Read(handlers).Matches(_lastHandled))
        {
            yield break;
        }

        foreach (var messageType in _pending)
        {
            yield return messageType;
        }
    }

    /// <summary>
    /// Marks the current end of the pending list, so that <see cref="Update"/> can tell the message
    /// types this scan adds from the ones earlier scans left unroutable.
    /// </summary>
    internal int MarkPending() => _pending.Count;

    internal void Add(Type messageType)
    {
        if (!_routable.Contains(messageType))
        {
            _routable.Add(messageType);
        }
    }

    internal void Add(Assembly assembly, IEnumerable<Type> types)
    {
        if (!_scannedAssemblies.Add(assembly))
        {
            return;
        }

        _pending.AddRange(types
            .Where(MessageTypes.IsConcreteMessage)
            .OrderBy(static type => type.FullName, StringComparer.Ordinal));
    }

    /// <summary>
    /// Promotes the message types this scan made routable, and remembers the registrations they were
    /// resolved against.
    /// </summary>
    /// <param name="mark">The value <see cref="MarkPending"/> returned before this scan added its types.</param>
    /// <param name="handled">
    /// The messages handled once this scan registered its handlers. The scan owns this instance and
    /// stops extending it once it returns, so it is retained as the comparison for the next scan.
    /// </param>
    internal void Update(int mark, HandledMessages handled)
    {
        // Earlier scans left types pending against fewer registrations, so they are reconsidered only
        // when the handled messages changed.
        var startIndex = handled.Matches(_lastHandled) ? mark : 0;

        // Resolved types are compacted out of the pending list rather than removed one by one.
        var remaining = startIndex;
        for (var index = startIndex; index < _pending.Count; index++)
        {
            var messageType = _pending[index];
            if (handled.Contains(messageType))
            {
                continue;
            }

            if (handled.CanRouteBase(messageType))
            {
                _routable.Add(messageType);
                continue;
            }

            _pending[remaining++] = messageType;
        }

        _pending.RemoveRange(remaining, _pending.Count - remaining);
        _lastHandled = handled;
    }
}