using System.Diagnostics.CodeAnalysis;
using Dispatcher.DependencyInjection;

namespace Dispatcher;

/// <summary>
/// Creates the closed wrapper instances a registry route dispatches through. Runtime generic
/// construction is confined to this type.
/// </summary>
internal static class HandlerWrapperFactory
{
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal static RequestHandlerWrapper CreateRequestWrapper(
        Type wrapperType,
        params Type[] genericArguments) =>
        (RequestHandlerWrapper)Create(wrapperType, genericArguments);

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal static NotificationHandlerWrapper CreateNotificationWrapper(Type notificationType) =>
        (NotificationHandlerWrapper)Create(typeof(NotificationHandlerWrapper<>), [notificationType]);

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal static NotificationHandlerWrapper CreateOpenNotificationWrapper(
        Type notificationType,
        Type[] handlerTypes) =>
        (NotificationHandlerWrapper)Create(
            typeof(OpenNotificationHandlerWrapper<>),
            [notificationType],
            handlerTypes);

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    internal static NotificationHandlerWrapper CreateCompositeNotificationWrapper(
        Type handledNotificationType,
        Type notificationType,
        Type[] handlerTypes) =>
        (NotificationHandlerWrapper)Create(
            typeof(CompositeNotificationHandlerWrapper<,>),
            [handledNotificationType, notificationType],
            handlerTypes);

    /// <summary>
    /// Closes every open generic notification handler over the concrete notification type.
    /// </summary>
    /// <remarks>
    /// A handler whose generic constraints the notification does not satisfy cannot handle it and is
    /// left out.
    /// </remarks>
    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    internal static Type[] CloseNotificationHandlers(
        Type notificationType,
        IEnumerable<NotificationHandlerDescriptor> registrations)
    {
        var handlerTypes = new List<Type>();
        foreach (var registration in registrations)
        {
            try
            {
                handlerTypes.Add(registration.HandlerType.MakeGenericType(notificationType));
            }
            catch (ArgumentException)
            {
                // The concrete notification does not satisfy the handler's generic constraints.
            }
        }

        return handlerTypes.ToArray();
    }

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static object Create(Type wrapperType, Type[] genericArguments) =>
        Activator.CreateInstance(wrapperType.MakeGenericType(genericArguments))!;

    [RequiresDynamicCode(CompatibilityMessages.WrapperDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.WrapperTrimming)]
    private static object Create(Type wrapperType, Type[] genericArguments, Type[] handlerTypes) =>
        Activator.CreateInstance(wrapperType.MakeGenericType(genericArguments), [handlerTypes])!;
}