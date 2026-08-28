namespace Dispatcher;

internal abstract record HandlerDescriptor(Type MessageType, Type HandlerType);

internal sealed record QueryHandlerDescriptor(
    Type MessageType,
    Type ResponseType,
    Type HandlerType) : HandlerDescriptor(MessageType, HandlerType);

internal sealed record CommandWithResponseHandlerDescriptor(
    Type MessageType,
    Type ResponseType,
    Type HandlerType) : HandlerDescriptor(MessageType, HandlerType);

internal sealed record CommandHandlerDescriptor(
    Type MessageType,
    Type HandlerType) : HandlerDescriptor(MessageType, HandlerType);

internal sealed record NotificationHandlerDescriptor(
    Type MessageType,
    Type HandlerType,
    bool IsOpenGeneric) : HandlerDescriptor(MessageType, HandlerType);