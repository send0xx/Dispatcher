using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace Dispatcher;

/// <summary>
/// Represents an immutable handler registry used by the reflection-based Dispatcher implementation.
/// </summary>
public sealed class DispatcherRegistry
{
    internal FrozenDictionary<Type, RequestHandlerWrapper> RequestHandlers { get; }
    internal FrozenDictionary<Type, NotificationHandlerWrapper> NotificationHandlers { get; }

    internal DispatcherRegistry(
        FrozenDictionary<Type, RequestHandlerWrapper> requestHandlers,
        FrozenDictionary<Type, NotificationHandlerWrapper> notificationHandlers)
    {
        RequestHandlers = requestHandlers;
        NotificationHandlers = notificationHandlers;
    }

    /// <summary>
    /// Creates a registry from message and handler registration metadata.
    /// </summary>
    /// <param name="registrations">The message and handler registrations to include.</param>
    /// <param name="telemetry">The optional telemetry service used to instrument routed handlers.</param>
    /// <returns>A registry containing routes for the specified registrations.</returns>
    /// <remarks>When provided, the caller retains ownership of <paramref name="telemetry"/> and must dispose it.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="registrations"/> is <see langword="null"/>.</exception>
    /// <exception cref="DuplicateHandlerException">A query or command has multiple handlers.</exception>
    /// <exception cref="AmbiguousHandlerException">
    /// A concrete message matches multiple unrelated handled message types.
    /// </exception>
    [RequiresDynamicCode("Creating handler wrappers from registration metadata requires runtime generic construction.")]
    [RequiresUnreferencedCode("Creating handler wrappers from registration metadata is not trimming safe.")]
    public static DispatcherRegistry Create(
        IEnumerable<MessageRegistration> registrations,
        DispatcherTelemetry? telemetry) =>
        DispatcherRegistryFactory.Create(registrations, telemetry);
}