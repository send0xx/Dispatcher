using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dispatcher.Extensions.DependencyInjection;

public static class DispatcherServiceCollectionExtensions
{
    private static readonly HashSet<Type> HandlerInterfaces =
    [
        typeof(IQueryHandler<,>),
        typeof(ICommandHandler<,>),
        typeof(ICommandHandler<>),
        typeof(INotificationHandler<>)
    ];

    private static readonly HashSet<Type> BehaviorInterfaces =
    [
        typeof(IPipelineBehavior<,>)
    ];

    public static IServiceCollection AddDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(static provider =>
            DispatcherRegistry.Create(
                provider.GetServices<HandlerRegistration>(),
                provider.GetServices<PipelineBehaviorRegistration>()));
        services.TryAddScoped<Dispatcher>();
        services.TryAddScoped<IDispatcher>(static provider =>
            provider.GetRequiredService<Dispatcher>());
        services.TryAddScoped<IQueryDispatcher>(static provider =>
            provider.GetRequiredService<Dispatcher>());
        services.TryAddScoped<ICommandDispatcher>(static provider =>
            provider.GetRequiredService<Dispatcher>());
        services.TryAddScoped<INotificationPublisher>(static provider =>
            provider.GetRequiredService<Dispatcher>());

        return services;
    }

    public static IServiceCollection AddDispatcherHandlers<TAssemblyMarker>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped) =>
        services.AddDispatcherHandlers(typeof(TAssemblyMarker).Assembly, lifetime);

    public static IServiceCollection AddDispatcherHandlers(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return services.AddDispatcherHandlers(lifetime, assembly);
    }

    public static IServiceCollection AddDispatcherHandlers(
        this IServiceCollection services,
        params Assembly[] assemblies) =>
        services.AddDispatcherHandlers(ServiceLifetime.Scoped, assemblies);

    public static IServiceCollection AddDispatcherHandlers(
        this IServiceCollection services,
        ServiceLifetime lifetime,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);

            if (services.Any(descriptor =>
                    descriptor.ServiceType == typeof(ScannedAssembly) &&
                    descriptor.ImplementationInstance is ScannedAssembly scanned &&
                    scanned.Assembly == assembly))
            {
                continue;
            }

            services.AddSingleton(new ScannedAssembly(assembly));
            RegisterHandlers(services, assembly, lifetime);
        }

        return services;
    }

    public static IServiceCollection AddPipelineBehavior<TBehavior>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped) =>
        services.AddPipelineBehavior(typeof(TBehavior), lifetime);

    public static IServiceCollection AddPipelineBehavior(
        this IServiceCollection services,
        Type behaviorType,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(behaviorType);

        if (!behaviorType.IsClass || behaviorType.IsAbstract)
        {
            throw new ArgumentException(
                $"Pipeline behavior '{behaviorType.FullName}' must be a non-abstract class.",
                nameof(behaviorType));
        }

        var serviceTypes = behaviorType.GetInterfaces()
            .Where(IsBehaviorInterface)
            .Select(type => behaviorType.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type)
            .Distinct()
            .ToArray();

        if (serviceTypes.Length == 0)
        {
            throw new ArgumentException(
                $"Pipeline behavior '{behaviorType.FullName}' does not implement a supported behavior interface.",
                nameof(behaviorType));
        }

        foreach (var serviceType in serviceTypes)
        {
            services.Add(ServiceDescriptor.Describe(serviceType, behaviorType, lifetime));
            services.AddSingleton(new PipelineBehaviorRegistration(
                serviceType,
                lifetime != ServiceLifetime.Transient));
        }

        return services;
    }

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
            return new HandlerRegistration(arguments[0], arguments[1], HandlerKind.Query, handlerType);
        }

        if (definition == typeof(ICommandHandler<,>))
        {
            return new HandlerRegistration(arguments[0], arguments[1], HandlerKind.CommandWithResponse, handlerType);
        }

        if (definition == typeof(ICommandHandler<>))
        {
            return new HandlerRegistration(arguments[0], null, HandlerKind.Command, handlerType);
        }

        return new HandlerRegistration(arguments[0], null, HandlerKind.Notification, handlerType);
    }

    private static bool IsHandlerInterface(Type type) =>
        type.IsGenericType && HandlerInterfaces.Contains(type.GetGenericTypeDefinition());

    private static bool IsBehaviorInterface(Type type) =>
        type.IsGenericType && BehaviorInterfaces.Contains(type.GetGenericTypeDefinition());

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