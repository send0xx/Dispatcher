using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dispatcher.Extensions.Microsoft.DependencyInjection;

/// <summary>
/// Provides Microsoft dependency injection registration methods for Dispatcher.
/// </summary>
public static class DispatcherServiceCollectionExtensions
{
    /// <summary>
    /// Registers Dispatcher services. Handlers must be registered separately.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(static provider =>
            DispatcherRegistry.CreatePrepared(provider.GetServices<HandlerRegistration>()));
        services.TryAddScoped<Dispatcher>();
        services.TryAddScoped<IDispatcher>(static provider =>
            provider.GetRequiredService<Dispatcher>());
        services.TryAddScoped<IQueryDispatcher>(static provider =>
            provider.GetRequiredService<Dispatcher>());
        services.TryAddScoped<ICommandDispatcher>(static provider =>
            provider.GetRequiredService<Dispatcher>());
        services.TryAddScoped<INotificationDispatcher>(static provider =>
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
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
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
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
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
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
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
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    public static IServiceCollection AddDispatcherHandlers(
        this IServiceCollection services,
        ServiceLifetime lifetime,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        return HandlerAssemblyScanner.Register(services, assemblies, lifetime);
    }

    /// <summary>
    /// Registers a pipeline behavior.
    /// </summary>
    /// <typeparam name="TBehavior">The behavior implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The behavior lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    [RequiresDynamicCode(CompatibilityMessages.BehaviorDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.BehaviorTrimming)]
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
    [RequiresDynamicCode(CompatibilityMessages.BehaviorDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.BehaviorTrimming)]
    public static IServiceCollection AddPipelineBehavior(
        this IServiceCollection services,
        Type behaviorType,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(behaviorType);

        return PipelineBehaviorTypeRegistrar.Register(services, behaviorType, lifetime);
    }
}