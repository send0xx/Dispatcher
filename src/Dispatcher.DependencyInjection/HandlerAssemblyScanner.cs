using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

internal static class HandlerAssemblyScanner
{
    private static readonly HashSet<Type> HandlerInterfaces =
    [
        typeof(IQueryHandler<,>),
        typeof(ICommandHandler<,>),
        typeof(ICommandHandler<>),
        typeof(INotificationHandler<>)
    ];

    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    internal static IServiceCollection Register(
        IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ServiceLifetime lifetime)
    {
        foreach (var assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);

            if (IsRegistered(services, assembly))
            {
                continue;
            }

            services.AddSingleton(new ScannedAssembly(assembly));
            RegisterHandlers(services, assembly, lifetime);
        }

        return services;
    }

    private static bool IsRegistered(IServiceCollection services, Assembly assembly) =>
        services.Any(descriptor =>
            descriptor.ServiceType == typeof(ScannedAssembly) &&
            descriptor.ImplementationInstance is ScannedAssembly scanned &&
            scanned.Assembly == assembly);

    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static void RegisterHandlers(
        IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime)
    {
        foreach (var implementationType in GetLoadableTypes(assembly)
                     .Where(type => type is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false })
                     .OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            foreach (var serviceType in implementationType.GetInterfaces()
                         .Where(IsHandlerInterface)
                         .OrderBy(type => type.FullName, StringComparer.Ordinal))
            {
                services.Add(ServiceDescriptor.Describe(serviceType, implementationType, lifetime));
                services.AddSingleton(CreateRegistration(serviceType, implementationType));
            }
        }
    }

    private static HandlerRegistration CreateRegistration(Type serviceType, Type handlerType)
    {
        var definition = serviceType.GetGenericTypeDefinition();
        var arguments = serviceType.GetGenericArguments();

        if (definition == typeof(IQueryHandler<,>))
        {
            return new QueryHandlerRegistration(arguments[0], arguments[1], handlerType);
        }

        if (definition == typeof(ICommandHandler<,>))
        {
            return new CommandWithResponseHandlerRegistration(arguments[0], arguments[1], handlerType);
        }

        if (definition == typeof(ICommandHandler<>))
        {
            return new CommandHandlerRegistration(arguments[0], handlerType);
        }

        return new NotificationHandlerRegistration(arguments[0], handlerType);
    }

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && HandlerInterfaces.Contains(type.GetGenericTypeDefinition());

    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(static type => type is not null)!;
        }
    }

    private sealed record ScannedAssembly(Assembly Assembly);
}