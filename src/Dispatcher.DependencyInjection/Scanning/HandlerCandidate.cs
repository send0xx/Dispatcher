namespace Dispatcher.DependencyInjection;

/// <summary>
/// A handler type found in a scanned assembly, with the service type it is registered as and the
/// handled-message descriptor it contributes to registry construction.
/// </summary>
/// <param name="ImplementationType">The handler implementation type.</param>
/// <param name="ServiceType">
/// The service type the handler is registered as. This is the handler interface for a closed handler,
/// and the handler type itself for an open generic notification handler.
/// </param>
/// <param name="Descriptor">The descriptor identifying the handled message.</param>
internal readonly record struct HandlerCandidate(
    Type ImplementationType,
    Type ServiceType,
    HandlerDescriptor Descriptor)
{
    internal bool IsOpenNotificationHandler => Descriptor is NotificationHandlerDescriptor { IsOpenGeneric: true };
}