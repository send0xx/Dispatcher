namespace Dispatcher;

/// <summary>
/// The exception thrown when a query or command has no registered handler.
/// </summary>
/// <param name="messageType">The message type without a handler.</param>
public sealed class HandlerNotFoundException(Type messageType)
    : InvalidOperationException($"No handler is registered for message type '{messageType.FullName}'.")
{
    /// <summary>
    /// Gets the message type without a handler.
    /// </summary>
    public Type MessageType { get; } = messageType;
}