using System.Diagnostics.CodeAnalysis;
using Dispatcher.DependencyInjection;

namespace Dispatcher;

internal abstract record HandlerDescriptor(Type MessageType, Type HandlerType);

/// <summary>
/// A handler for a query or a command, which are the messages that route to exactly one handler and
/// dispatch through a request wrapper.
/// </summary>
internal abstract record RequestHandlerDescriptor(Type MessageType, Type HandlerType)
    : HandlerDescriptor(MessageType, HandlerType)
{
    /// <summary>
    /// Creates the wrapper that resolves and invokes this handler.
    /// </summary>
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal abstract RequestHandlerWrapper CreateWrapper();

    /// <summary>
    /// Determines whether a concrete message type can route to this handler, which requires the
    /// message to declare the same response shape the handler returns.
    /// </summary>
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    internal abstract bool CanRoute(Type messageType);
}

internal sealed record QueryHandlerDescriptor(
    Type MessageType,
    Type ResponseType,
    Type HandlerType) : RequestHandlerDescriptor(MessageType, HandlerType)
{
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal override RequestHandlerWrapper CreateWrapper() =>
        HandlerWrapperFactory.CreateRequestWrapper(
            typeof(QueryHandlerWrapper<,>),
            MessageType,
            ResponseType);

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    internal override bool CanRoute(Type messageType) =>
        typeof(IQuery<>).MakeGenericType(ResponseType).IsAssignableFrom(messageType);
}

internal sealed record CommandWithResponseHandlerDescriptor(
    Type MessageType,
    Type ResponseType,
    Type HandlerType) : RequestHandlerDescriptor(MessageType, HandlerType)
{
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal override RequestHandlerWrapper CreateWrapper() =>
        HandlerWrapperFactory.CreateRequestWrapper(
            typeof(CommandWithResponseHandlerWrapper<,>),
            MessageType,
            ResponseType);

    // A resultless command is adapted to Unit only inside the pipeline, so it must not route to a
    // handler that declares a response type.
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    internal override bool CanRoute(Type messageType) =>
        !typeof(ICommand).IsAssignableFrom(messageType) &&
        typeof(ICommand<>).MakeGenericType(ResponseType).IsAssignableFrom(messageType);
}

internal sealed record CommandHandlerDescriptor(
    Type MessageType,
    Type HandlerType) : RequestHandlerDescriptor(MessageType, HandlerType)
{
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal override RequestHandlerWrapper CreateWrapper() =>
        HandlerWrapperFactory.CreateRequestWrapper(typeof(CommandHandlerWrapper<>), MessageType);

    internal override bool CanRoute(Type messageType) =>
        typeof(ICommand).IsAssignableFrom(messageType);
}

internal sealed record NotificationHandlerDescriptor(
    Type MessageType,
    Type HandlerType,
    bool IsOpenGeneric) : HandlerDescriptor(MessageType, HandlerType);