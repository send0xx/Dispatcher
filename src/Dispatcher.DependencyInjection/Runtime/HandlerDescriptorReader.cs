using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher;

/// <summary>
/// Reads the handlers a service collection registers, whichever path registered them.
/// </summary>
internal static class HandlerDescriptorReader
{
    internal static HandlerDescriptor[] Read(IEnumerable<ServiceDescriptor> services) =>
        services
            .Select(TryCreate)
            .Where(static descriptor => descriptor is not null)
            .Cast<HandlerDescriptor>()
            .ToArray();

    internal static HandlerDescriptor? TryCreate(ServiceDescriptor descriptor)
    {
        if (descriptor.IsKeyedService)
        {
            return null;
        }

        // A service registered as an instance carries no implementation type, but its runtime type
        // identifies the handler just as well.
        var serviceType = descriptor.ServiceType;
        var handlerType = descriptor.ImplementationType ??
            descriptor.ImplementationInstance?.GetType() ??
            serviceType;
        if (HandlerDescriptorFactory.IsHandlerInterface(serviceType))
        {
            return HandlerDescriptorFactory.Create(serviceType, handlerType);
        }

        // An open generic notification handler is registered as itself rather than as
        // INotificationHandler<>, so it is the service type that identifies it.
        return serviceType == handlerType
            ? HandlerDescriptorFactory.CreateOpenNotification(handlerType)
            : null;
    }
}