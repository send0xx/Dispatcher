namespace Dispatcher.DependencyInjection;

/// <summary>
/// A handler type found in a scanned assembly, with the service type it is registered as and the
/// metadata it contributes to the registry.
/// </summary>
/// <param name="ImplementationType">The handler implementation type.</param>
/// <param name="ServiceType">
/// The service type the handler is registered as. This is the handler interface for a closed handler,
/// and the handler type itself for an open generic notification handler.
/// </param>
/// <param name="Registration">The registration metadata describing the handled message.</param>
internal readonly record struct HandlerCandidate(
    Type ImplementationType,
    Type ServiceType,
    HandlerRegistration Registration)
{
    internal bool IsOpenNotificationHandler => Registration is NotificationHandlerRegistration { IsOpenGeneric: true };
}