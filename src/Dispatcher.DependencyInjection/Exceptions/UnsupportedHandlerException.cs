namespace Dispatcher;

/// <summary>
/// The exception thrown when assembly scanning finds handlers that cannot be registered.
/// </summary>
public sealed class UnsupportedHandlerException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedHandlerException"/> class.
    /// </summary>
    /// <param name="handlers">
    /// The offending handler types, each mapped to the reason it cannot be registered.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="handlers"/> is <see langword="null"/>.</exception>
    public UnsupportedHandlerException(IReadOnlyDictionary<Type, string> handlers)
        : base(CreateMessage(handlers))
    {
        Handlers = new Dictionary<Type, string>(handlers);
    }

    /// <summary>
    /// Gets the handler types that cannot be registered.
    /// </summary>
    /// <value>Each offending handler type mapped to the reason it cannot be registered.</value>
    public IReadOnlyDictionary<Type, string> Handlers { get; }

    private static string CreateMessage(IReadOnlyDictionary<Type, string> handlers)
    {
        ArgumentNullException.ThrowIfNull(handlers);

        var reasons = handlers
            .OrderBy(static handler => handler.Key.FullName, StringComparer.Ordinal)
            .Select(static handler => $"{Environment.NewLine}  - '{handler.Key.FullName}' {handler.Value}");
        return "One or more scanned handlers cannot be registered:" + string.Concat(reasons);
    }
}