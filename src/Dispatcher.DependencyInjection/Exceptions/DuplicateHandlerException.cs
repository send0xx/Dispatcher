namespace Dispatcher;

/// <summary>
/// The exception thrown when a query or command has multiple registered handlers.
/// </summary>
/// <param name="messageType">The message type with duplicate handlers.</param>
/// <param name="firstHandlerType">The first registered handler type.</param>
/// <param name="secondHandlerType">The second registered handler type.</param>
public sealed class DuplicateHandlerException(
    Type messageType,
    Type firstHandlerType,
    Type secondHandlerType)
    : InvalidOperationException(
        $"Multiple handlers are registered for message type '{messageType.FullName}': " +
        $"'{firstHandlerType.FullName}' and '{secondHandlerType.FullName}'.")
{
    /// <summary>
    /// Gets the message type with duplicate handlers.
    /// </summary>
    /// <value>The concrete type of the query or command with duplicate handlers.</value>
    public Type MessageType { get; } = messageType;

    /// <summary>
    /// Gets the first registered handler type.
    /// </summary>
    /// <value>The type of the handler that was registered first.</value>
    public Type FirstHandlerType { get; } = firstHandlerType;

    /// <summary>
    /// Gets the second registered handler type.
    /// </summary>
    /// <value>The type of the handler that caused the duplicate registration.</value>
    public Type SecondHandlerType { get; } = secondHandlerType;
}