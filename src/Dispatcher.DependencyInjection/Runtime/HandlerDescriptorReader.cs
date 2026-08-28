using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher;

internal static class HandlerDescriptorReader
{
    private static readonly HashSet<Type> HandlerInterfaces =
    [
        typeof(IQueryHandler<,>),
        typeof(ICommandHandler<,>),
        typeof(ICommandHandler<>),
        typeof(INotificationHandler<>)
    ];

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

        var serviceType = descriptor.ServiceType;
        var handlerType = descriptor.ImplementationType ??
            descriptor.ImplementationInstance?.GetType() ??
            serviceType;
        if (serviceType.IsGenericType &&
            HandlerInterfaces.Contains(serviceType.GetGenericTypeDefinition()))
        {
            return CreateClosed(serviceType, handlerType);
        }

        return serviceType.IsGenericTypeDefinition && serviceType == handlerType
            ? CreateOpenNotification(serviceType)
            : null;
    }

    private static HandlerDescriptor CreateClosed(Type serviceType, Type handlerType)
    {
        var definition = serviceType.GetGenericTypeDefinition();
        var arguments = serviceType.GetGenericArguments();
        if (definition == typeof(IQueryHandler<,>))
        {
            return new QueryHandlerDescriptor(arguments[0], arguments[1], handlerType);
        }

        if (definition == typeof(ICommandHandler<,>))
        {
            return new CommandWithResponseHandlerDescriptor(arguments[0], arguments[1], handlerType);
        }

        if (definition == typeof(ICommandHandler<>))
        {
            return new CommandHandlerDescriptor(arguments[0], handlerType);
        }

        return new NotificationHandlerDescriptor(arguments[0], handlerType, false);
    }

    private static HandlerDescriptor? CreateOpenNotification(Type handlerType)
    {
        if (handlerType.GetGenericArguments() is not [var parameter])
        {
            return null;
        }

        var handlesParameter = handlerType.GetInterfaces().Any(serviceType =>
            serviceType.IsGenericType &&
            serviceType.GetGenericTypeDefinition() == typeof(INotificationHandler<>) &&
            serviceType.GetGenericArguments()[0] == parameter);
        return handlesParameter
            ? new NotificationHandlerDescriptor(parameter, handlerType, true)
            : null;
    }
}