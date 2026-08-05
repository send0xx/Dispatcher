namespace Dispatcher;

public sealed record HandlerRegistration(
    Type MessageType,
    Type? ResponseType,
    HandlerKind Kind,
    Type HandlerType);