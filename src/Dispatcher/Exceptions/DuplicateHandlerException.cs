namespace Dispatcher;

public sealed class DuplicateHandlerException(
    Type messageType,
    Type firstHandlerType,
    Type secondHandlerType)
    : InvalidOperationException(
        $"Multiple handlers are registered for message type '{messageType.FullName}': " +
        $"'{firstHandlerType.FullName}' and '{secondHandlerType.FullName}'.")
{
    public Type MessageType { get; } = messageType;
    public Type FirstHandlerType { get; } = firstHandlerType;
    public Type SecondHandlerType { get; } = secondHandlerType;
}
