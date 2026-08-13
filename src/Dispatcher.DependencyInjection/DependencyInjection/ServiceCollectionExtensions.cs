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
    /// <param name="services">The service collection to add Dispatcher services to.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    [RequiresDynamicCode(CompatibilityMessages.DispatcherDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.DispatcherTrimming)]
    public static IServiceCollection AddDispatcher(this IServiceCollection services) =>
        AddDispatcher(services, static _ => { });

    /// <summary>
    /// Registers Dispatcher services using the specified options. Handlers must be registered separately.
    /// </summary>
    /// <param name="services">The service collection to add Dispatcher services to.</param>
    /// <param name="configure">The delegate that configures Dispatcher services.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="configure"/> sets <see cref="DispatcherOptions.ServiceLifetime"/> to
    /// <see cref="ServiceLifetime.Singleton"/>.
    /// </exception>
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
        }
        services.TryAddSingleton(static provider =>
            DispatcherRegistry.Create(
                provider.GetServices<MessageRegistration>()
                    .Concat(provider.GetServices<HandlerRegistration>()),
                provider.GetService<DispatcherTelemetry>()));
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
    /// <typeparam name="TAssemblyMarker">A type from the assembly that contains the handlers.</typeparam>
    /// <param name="services">The service collection to add the handlers to.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    public static IServiceCollection AddDispatcherHandlers<TAssemblyMarker>(this IServiceCollection services) =>
        services.AddDispatcherHandlers(typeof(TAssemblyMarker).Assembly);

    /// <summary>
    /// Registers handlers found in the assembly containing <typeparamref name="TAssemblyMarker"/>
    /// using the specified options.
    /// </summary>
    /// <typeparam name="TAssemblyMarker">A type from the assembly that contains the handlers.</typeparam>
    /// <param name="services">The service collection to add the handlers to.</param>
    /// <param name="configure">The delegate that configures handler registration.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    public static IServiceCollection AddDispatcherHandlers<TAssemblyMarker>(
        this IServiceCollection services,
        Action<DispatcherOptions> configure) =>
        services.AddDispatcherHandlers(typeof(TAssemblyMarker).Assembly, configure);

    /// <summary>
    /// Registers handlers found in an assembly.
    /// </summary>
    /// <param name="services">The service collection to add the handlers to.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="assembly"/> is <see langword="null"/>.
    /// </exception>
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    public static IServiceCollection AddDispatcherHandlers(
        this IServiceCollection services,
        Assembly assembly) =>
        services.AddDispatcherHandlers(static _ => { }, assembly);

    /// <summary>
    /// Registers handlers found in an assembly using the specified options.
    /// </summary>
    /// <param name="services">The service collection to add the handlers to.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <param name="configure">The delegate that configures handler registration.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/>, <paramref name="assembly"/>, or <paramref name="configure"/> is
    /// <see langword="null"/>.
    /// </exception>
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
    /// Registers handlers found in one or more assemblies.
    /// </summary>
    /// <param name="services">The service collection to add the handlers to.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="assemblies"/> is <see langword="null"/>, or
    /// <paramref name="assemblies"/> contains a <see langword="null"/> element.
    /// </exception>
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    public static IServiceCollection AddDispatcherHandlers(
        this IServiceCollection services,
        params Assembly[] assemblies) =>
        services.AddDispatcherHandlers(static _ => { }, assemblies);

    /// <summary>
    /// Registers handlers found in one or more assemblies.
    /// </summary>
    /// <param name="services">The service collection to add the handlers to.</param>
    /// <param name="configure">The delegate that configures handler registration.</param>
    /// <param name="assemblies">The assemblies to scan.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/>, <paramref name="configure"/>, or <paramref name="assemblies"/> is
    /// <see langword="null"/>, or <paramref name="assemblies"/> contains a <see langword="null"/> element.
    /// </exception>
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
    /// <typeparam name="TBehavior">The type of pipeline behavior to register.</typeparam>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TBehavior"/> is not a supported concrete or open generic behavior class.
    /// </exception>
    [RequiresDynamicCode(CompatibilityMessages.BehaviorDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.BehaviorTrimming)]
    public static IServiceCollection AddPipelineBehavior<TBehavior>(this IServiceCollection services) =>
        services.AddPipelineBehavior(typeof(TBehavior));

    /// <summary>
    /// Registers a pipeline behavior using the specified options.
    /// </summary>
    /// <typeparam name="TBehavior">The type of pipeline behavior to register.</typeparam>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <param name="configure">The delegate that configures behavior registration.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TBehavior"/> is not a supported concrete or open generic behavior class.
    /// </exception>
    [RequiresDynamicCode(CompatibilityMessages.BehaviorDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.BehaviorTrimming)]
    public static IServiceCollection AddPipelineBehavior<TBehavior>(
        this IServiceCollection services,
        Action<DispatcherOptions> configure) =>
        services.AddPipelineBehavior(typeof(TBehavior), configure);

    /// <summary>
    /// Registers a pipeline behavior.
    /// </summary>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <param name="behaviorType">The behavior implementation type, which may be an open generic type.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="behaviorType"/> is <see langword="null"/>.
    /// </exception>
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
    /// Registers a pipeline behavior using the specified options.
    /// </summary>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <param name="behaviorType">The behavior implementation type, which may be an open generic type.</param>
    /// <param name="configure">The delegate that configures behavior registration.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/>, <paramref name="behaviorType"/>, or <paramref name="configure"/> is
    /// <see langword="null"/>.
    /// </exception>
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