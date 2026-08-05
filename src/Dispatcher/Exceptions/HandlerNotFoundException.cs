namespace Dispatcher;

public sealed class HandlerNotFoundException(Type messageType)
    : InvalidOperationException($"No handler is registered for message type '{messageType.FullName}'.")
{
    public Type MessageType { get; } = messageType;
}