namespace Dispatcher;

/// <summary>
/// Identifies the kind of handler represented by a registration.
/// </summary>
public enum HandlerKind
{
    /// <summary>
    /// A query handler.
    /// </summary>
    Query,

    /// <summary>
    /// A resultless command handler.
    /// </summary>
    Command,

    /// <summary>
    /// A result-bearing command handler.
    /// </summary>
    CommandWithResponse,

    /// <summary>
    /// A notification handler.
    /// </summary>
    Notification
}