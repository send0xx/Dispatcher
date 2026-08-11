using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// Provides Microsoft dependency injection registration methods for Dispatcher.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Dispatcher services. Handlers must be registered separately.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresDynamicCode(CompatibilityMessages.DispatcherDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.DispatcherTrimming)]
    public static IServiceCollection AddDispatcher(this IServiceCollection services) =>
        AddDispatcher(services, static _ => { });

    /// <summary>
    /// Registers Dispatcher services with the specified options. Handlers must be registered separately.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The options used to configure Dispatcher services.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresDynamicCode(CompatibilityMessages.DispatcherDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.DispatcherTrimming)]
    public static IServiceCollection AddDispatcher(
        this IServiceCollection services,
        Action<DispatcherOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DispatcherOptions();
        configure(options);
        if (options.ServiceLifetime == ServiceLifetime.Singleton)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configure),
                options.ServiceLifetime,
                "A singleton dispatcher would capture the root service provider.");
        }

        var telemetryOptions = options.Telemetry;
        if (telemetryOptions.EnableMetrics || telemetryOptions.EnableTracing)
        {
            var telemetryConfiguration = new DispatcherTelemetryOptions
            {
                EnableMetrics = telemetryOptions.EnableMetrics,
                EnableTracing = telemetryOptions.EnableTracing,
                MeterName = telemetryOptions.MeterName,
                ActivitySourceName = telemetryOptions.ActivitySourceName
            };
            services.TryAddSingleton(_ => new DispatcherTelemetry(telemetryConfiguration));
            services.TryAddSingleton(static provider =>
                DispatcherRegistry.Create(
                    provider.GetServices<HandlerRegistration>(),
                    provider.GetRequiredService<DispatcherTelemetry>()));
        }
        else
        {
            services.TryAddSingleton(static provider =>
                DispatcherRegistry.Create(provider.GetServices<HandlerRegistration>()));
        }
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(Dispatcher),
            typeof(Dispatcher),
            options.ServiceLifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(IDispatcher),
            static provider => provider.GetRequiredService<Dispatcher>(),
            options.ServiceLifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(IQueryDispatcher),
            static provider => provider.GetRequiredService<Dispatcher>(),
            options.ServiceLifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(ICommandDispatcher),
            static provider => provider.GetRequiredService<Dispatcher>(),
            options.ServiceLifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(INotificationDispatcher),
            static provider => provider.GetRequiredService<Dispatcher>(),
            options.ServiceLifetime));

        return services;
    }

    /// <summary>
    /// Registers handlers found in the assembly containing <typeparamref name="TAssemblyMarker"/>.
    /// </summary>
    /// <typeparam name="TAssemblyMarker">A type whose assembly contains handlers.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    public static IServiceCollection AddDispatcherHandlers<TAssemblyMarker>(this IServiceCollection services) =>
        services.AddDispatcherHandlers(typeof(TAssemblyMarker).Assembly);

    /// <summary>
    /// Registers handlers found in the assembly containing <typeparamref name="TAssemblyMarker"/>
    /// with the specified options.
    /// </summary>
    /// <typeparam name="TAssemblyMarker">A type whose assembly contains handlers.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The options used to configure handler registration.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    public static IServiceCollection AddDispatcherHandlers<TAssemblyMarker>(
        this IServiceCollection services,
        Action<DispatcherOptions> configure) =>
        services.AddDispatcherHandlers(typeof(TAssemblyMarker).Assembly, configure);

    /// <summary>
    /// Registers handlers found in an assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    public static IServiceCollection AddDispatcherHandlers(
        this IServiceCollection services,
        Assembly assembly) =>
        services.AddDispatcherHandlers(static _ => { }, assembly);

    /// <summary>
    /// Registers handlers found in an assembly with the specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="configure">The options used to configure handler registration.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    public static IServiceCollection AddDispatcherHandlers(
        this IServiceCollection services,
        Assembly assembly,
        Action<DispatcherOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return services.AddDispatcherHandlers(configure, assembly);
    }

    /// <summary>
    /// Registers scoped handlers found in one or more assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    public static IServiceCollection AddDispatcherHandlers(
        this IServiceCollection services,
        params Assembly[] assemblies) =>
        services.AddDispatcherHandlers(static _ => { }, assemblies);

    /// <summary>
    /// Registers handlers found in one or more assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The options used to configure handler registration.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    public static IServiceCollection AddDispatcherHandlers(
        this IServiceCollection services,
        Action<DispatcherOptions> configure,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(assemblies);

        var options = new DispatcherOptions();
        configure(options);

        return HandlerAssemblyScanner.Register(services, assemblies, options.ServiceLifetime);
    }

    /// <summary>
    /// Registers a pipeline behavior.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresDynamicCode(CompatibilityMessages.BehaviorDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.BehaviorTrimming)]
    public static IServiceCollection AddPipelineBehavior<TBehavior>(this IServiceCollection services) =>
        services.AddPipelineBehavior(typeof(TBehavior));

    /// <summary>
    /// Registers a pipeline behavior with the specified options.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The options used to configure behavior registration.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresDynamicCode(CompatibilityMessages.BehaviorDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.BehaviorTrimming)]
    public static IServiceCollection AddPipelineBehavior<TBehavior>(
        this IServiceCollection services,
        Action<DispatcherOptions> configure) =>
        services.AddPipelineBehavior(typeof(TBehavior), configure);

    /// <summary>
    /// Registers a pipeline behavior.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="behaviorType">The behavior implementation type, which may be an open generic type.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="behaviorType"/> is not a supported concrete or open generic behavior class.
    /// </exception>
    [RequiresDynamicCode(CompatibilityMessages.BehaviorDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.BehaviorTrimming)]
    public static IServiceCollection AddPipelineBehavior(
        this IServiceCollection services,
        Type behaviorType) =>
        services.AddPipelineBehavior(behaviorType, static _ => { });

    /// <summary>
    /// Registers a pipeline behavior with the specified options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="behaviorType">The behavior implementation type, which may be an open generic type.</param>
    /// <param name="configure">The options used to configure behavior registration.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="behaviorType"/> is not a supported concrete or open generic behavior class.
    /// </exception>
    [RequiresDynamicCode(CompatibilityMessages.BehaviorDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.BehaviorTrimming)]
    public static IServiceCollection AddPipelineBehavior(
        this IServiceCollection services,
        Type behaviorType,
        Action<DispatcherOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(behaviorType);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DispatcherOptions();
        configure(options);

        return PipelineBehaviorTypeRegistrar.Register(
            services,
            behaviorType,
            options.ServiceLifetime);
    }
}
