namespace Dispatcher;

/// <summary>
/// The exception thrown when a message matches multiple unrelated handled message types.
/// </summary>
/// <param name="messageType">The concrete message type being routed.</param>
/// <param name="candidateMessageTypes">The equally specific handled message types.</param>
public sealed class AmbiguousHandlerException(
    Type messageType,
    IReadOnlyCollection<Type> candidateMessageTypes)
    : InvalidOperationException(CreateMessage(messageType, candidateMessageTypes))
{
    /// <summary>
    /// Gets the concrete message type being routed.
    /// </summary>
    /// <value>The concrete query, command, or notification type.</value>
    public Type MessageType { get; } = messageType;

    /// <summary>
    /// Gets the equally specific handled message types.
    /// </summary>
    /// <value>The handled message types that make the route ambiguous.</value>
    public IReadOnlyList<Type> CandidateMessageTypes { get; } = [.. candidateMessageTypes];

    private static string CreateMessage(
        Type messageType,
        IReadOnlyCollection<Type> candidateMessageTypes)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(candidateMessageTypes);

        var candidates = string.Join(
            ", ",
            candidateMessageTypes
                .Select(static type => $"'{type.FullName}'")
                .OrderBy(static name => name, StringComparer.Ordinal));
        return $"Message type '{messageType.FullName}' matches multiple equally specific handled message types: " +
            candidates + ".";
    }
}