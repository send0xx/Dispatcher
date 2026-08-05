namespace Dispatcher;

public enum HandlerKind
{
    Query,
    Command,
    CommandWithResponse,
    Notification
}

public sealed record HandlerRegistration(
    Type MessageType,
    Type? ResponseType,
    HandlerKind Kind,
    Type HandlerType);
