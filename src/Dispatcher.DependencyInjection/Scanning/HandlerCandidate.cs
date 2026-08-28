using System.Reflection;

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
    /// <summary>
    /// Gets the assemblies declaring the messages this handler can handle. They are scanned for route
    /// targets as well, because a message that the handler routes may be declared by an assembly that
    /// contains no handler of its own.
    /// </summary>
    internal IEnumerable<Assembly> GetMessageAssemblies()
    {
        if (Descriptor is not NotificationHandlerDescriptor { IsOpenGeneric: true })
        {
            yield return Descriptor.MessageType.Assembly;
            yield break;
        }

        // An open generic handler names no message type, so its type parameter constraints describe
        // the notifications it can handle.
        foreach (var constraint in ImplementationType
                     .GetGenericArguments()[0]
                     .GetGenericParameterConstraints())
        {
            yield return constraint.Assembly;
        }
    }
}