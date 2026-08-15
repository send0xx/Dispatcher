namespace Dispatcher;

/// <summary>
/// The exception thrown when assembly scanning finds handlers that cannot be registered.
/// </summary>
/// <param name="handlers">The offending handler types, each mapped to the reason it cannot be registered.</param>
public sealed class UnsupportedHandlerException(IReadOnlyDictionary<Type, string> handlers)
    : InvalidOperationException(CreateMessage(handlers))
{
    /// <summary>
    /// Gets the handler types that cannot be registered.
    /// </summary>
    /// <value>Each offending handler type mapped to the reason it cannot be registered.</value>
    public IReadOnlyDictionary<Type, string> Handlers { get; } = new Dictionary<Type, string>(handlers);

    private static string CreateMessage(IReadOnlyDictionary<Type, string> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var reasons = handlers
            .OrderBy(static handler => handler.Key.FullName, StringComparer.Ordinal)
            .Select(static handler => $"{Environment.NewLine}  - '{handler.Key.FullName}' {handler.Value}");
        return "One or more scanned handlers cannot be registered:" + string.Concat(reasons);
    }
}