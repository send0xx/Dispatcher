using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// Tracks which handler services a scan still needs to register.
/// </summary>
/// <remarks>
/// The service collection is read once per scan because typed and direct registrations may be added
/// between calls.
/// </remarks>
internal sealed class ExistingRegistrations
{
    private readonly HashSet<(Type ServiceType, Type ImplementationType)> _unregisteredServices = [];

    private ExistingRegistrations()
    {
    }

    /// <summary>
    /// The messages handled by the service collection, extended as the scan registers handlers.
    /// </summary>
    internal HandledMessages Handled { get; } = new();

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

    private void Read(ServiceDescriptor descriptor)
    {
        if (HandlerDescriptorReader.TryCreate(descriptor) is { } handlerDescriptor)
        {
            Handled.Add(handlerDescriptor);
        }

        // A service registered as an instance carries no implementation type, but its runtime type
        // identifies it just as well. A factory descriptor cannot be matched at all, because
        // Microsoft DI does not expose what a factory will return.
        if (!descriptor.IsKeyedService &&
            (descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType()) is
            { } implementationType)
        {
            _unregisteredServices.Remove((descriptor.ServiceType, implementationType));
        }
    }
}