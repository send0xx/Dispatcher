using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// What the service collection already contains that a scan needs to know about, read in one pass.
/// </summary>
/// <remarks>
/// A scan cannot cache this between calls, because the typed registration methods and direct
/// Microsoft DI registrations may add handlers and message metadata in between. Reading it once per
/// scan instead of once per question keeps that cost to a single pass.
/// </remarks>
internal sealed class ExistingRegistrations
{
    private readonly HashSet<(Type ServiceType, Type ImplementationType)> _unregisteredServices = [];
    private readonly HashSet<HandlerRegistration> _unregisteredHandlers = [];

    private ExistingRegistrations()
    {
    }

    /// <summary>
    /// Message types that have a handler, extended as the scan registers its own handlers.
    /// </summary>
    internal HashSet<Type> HandledMessageTypes { get; } = [];

    /// <summary>
    /// Message types that already have routing metadata, extended as the scan registers more.
    /// </summary>
    internal HashSet<Type> RegisteredMessageTypes { get; } = [];

    internal bool HasOpenNotificationHandler { get; private set; }

    /// <param name="services">The service collection being registered into.</param>
    /// <param name="candidates">
    /// The candidates this scan may need to add. Only these are tracked for existence, so the
    /// bookkeeping stays sized by the scanned handlers rather than by the whole service collection.
    /// </param>
    internal static ExistingRegistrations Read(
        IServiceCollection services,
        IEnumerable<HandlerCandidate> candidates)
    {
        var existing = new ExistingRegistrations();
        foreach (var candidate in candidates)
        {
            existing._unregisteredServices.Add((candidate.ServiceType, candidate.ImplementationType));
            existing._unregisteredHandlers.Add(candidate.Registration);
        }

        foreach (var descriptor in services)
        {
            existing.Read(descriptor);
        }

        return existing;
    }

    /// <summary>
    /// Claims the service descriptor for a candidate, returning whether this scan should add it.
    /// Claiming it once means a handler registered through another path, or listed twice by this
    /// scan, is never registered a second time.
    /// </summary>
    internal bool TryClaimServiceDescriptor(HandlerCandidate candidate) =>
        _unregisteredServices.Remove((candidate.ServiceType, candidate.ImplementationType));

    /// <summary>
    /// Claims the registration metadata for a candidate, returning whether this scan should add it.
    /// </summary>
    internal bool TryClaimRegistrationMetadata(HandlerCandidate candidate) =>
        _unregisteredHandlers.Remove(candidate.Registration);

    private void Read(ServiceDescriptor descriptor)
    {
        if (descriptor.ServiceType == typeof(HandlerRegistration) &&
            descriptor.ImplementationInstance is HandlerRegistration handlerRegistration)
        {
            if (handlerRegistration is NotificationHandlerRegistration { IsOpenGeneric: true })
            {
                HasOpenNotificationHandler = true;
            }
            else
            {
                HandledMessageTypes.Add(handlerRegistration.MessageType);
            }

            _unregisteredHandlers.Remove(handlerRegistration);
            return;
        }

        if (descriptor.ServiceType == typeof(MessageRegistration) &&
            descriptor.ImplementationInstance is MessageRegistration messageRegistration)
        {
            RegisteredMessageTypes.Add(messageRegistration.MessageType);
            return;
        }

        if (descriptor.ImplementationType is { } implementationType)
        {
            _unregisteredServices.Remove((descriptor.ServiceType, implementationType));
        }
    }
}