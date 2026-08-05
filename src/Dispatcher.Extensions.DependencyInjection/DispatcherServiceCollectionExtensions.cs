using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dispatcher.Extensions.DependencyInjection;

/// <summary>
/// Provides Microsoft dependency injection registration methods for Dispatcher.
/// </summary>
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

    /// <summary>
    /// Registers Dispatcher infrastructure without scanning for handlers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(static provider =>
            DispatcherRegistry.Create(provider.GetServices<HandlerRegistration>()));
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

    /// <summary>
    /// Registers handlers found in the assembly containing <typeparamref name="TAssemblyMarker"/>.
    /// </summary>
    /// <typeparam name="TAssemblyMarker">A type whose assembly contains handlers.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The lifetime assigned to discovered handlers.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDispatcherHandlers<TAssemblyMarker>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped) =>
        services.AddDispatcherHandlers(typeof(TAssemblyMarker).Assembly, lifetime);

    /// <summary>
    /// Registers handlers found in an assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="lifetime">The lifetime assigned to discovered handlers.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDispatcherHandlers(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return services.AddDispatcherHandlers(lifetime, assembly);
    }

    /// <summary>
    /// Registers scoped handlers found in one or more assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDispatcherHandlers(
        this IServiceCollection services,
        params Assembly[] assemblies) =>
        services.AddDispatcherHandlers(ServiceLifetime.Scoped, assemblies);

    /// <summary>
    /// Registers handlers found in one or more assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The lifetime assigned to discovered handlers.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The service collection for chaining.</returns>
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

    /// <summary>
    /// Registers a pipeline behavior.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The behavior lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPipelineBehavior<TBehavior>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped) =>
        services.AddPipelineBehavior(typeof(TBehavior), lifetime);

    /// <summary>
    /// Registers a pipeline behavior.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="behaviorType">The behavior implementation type, which may be an open generic type.</param>
    /// <param name="lifetime">The behavior lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="behaviorType"/> is not a concrete behavior class.</exception>
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