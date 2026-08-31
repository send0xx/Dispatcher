using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher;

/// <summary>
/// Provides typed Dispatcher registration methods for Microsoft dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a query handler.
    /// </summary>
    /// <typeparam name="TQuery">The type of query to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the query.</typeparam>
    /// <typeparam name="THandler">The type of query handler to register.</typeparam>
    /// <param name="services">The service collection to add the handler to.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddQueryHandler<TQuery, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services)
        where TQuery : IQuery<TResponse>
        where THandler : class, IQueryHandler<TQuery, TResponse> =>
        services.AddQueryHandler<TQuery, TResponse, THandler>(static _ => { });

    /// <summary>
    /// Registers a query handler with the specified options.
    /// </summary>
    /// <typeparam name="TQuery">The type of query to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the query.</typeparam>
    /// <typeparam name="THandler">The type of query handler to register.</typeparam>
    /// <param name="services">The service collection to add the handler to.</param>
    /// <param name="configure">The delegate that configures the handler registration.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddQueryHandler<TQuery, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        Action<DispatcherOptions> configure)
        where TQuery : IQuery<TResponse>
        where THandler : class, IQueryHandler<TQuery, TResponse> =>
        AddHandler<IQueryHandler<TQuery, TResponse>, THandler>(services, configure);

    /// <summary>
    /// Registers a handler for a command that returns a response.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the command.</typeparam>
    /// <typeparam name="THandler">The type of command handler to register.</typeparam>
    /// <param name="services">The service collection to add the handler to.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddCommandHandler<TCommand, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services)
        where TCommand : ICommand<TResponse>
        where THandler : class, ICommandHandler<TCommand, TResponse> =>
        services.AddCommandHandler<TCommand, TResponse, THandler>(static _ => { });

    /// <summary>
    /// Registers a handler for a command that returns a response, using the specified options.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the command.</typeparam>
    /// <typeparam name="THandler">The type of command handler to register.</typeparam>
    /// <param name="services">The service collection to add the handler to.</param>
    /// <param name="configure">The delegate that configures the handler registration.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddCommandHandler<TCommand, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        Action<DispatcherOptions> configure)
        where TCommand : ICommand<TResponse>
        where THandler : class, ICommandHandler<TCommand, TResponse> =>
        AddHandler<ICommandHandler<TCommand, TResponse>, THandler>(services, configure);

    /// <summary>
    /// Registers a handler for a command that does not return a response.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to handle.</typeparam>
    /// <typeparam name="THandler">The type of command handler to register.</typeparam>
    /// <param name="services">The service collection to add the handler to.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddCommandHandler<TCommand,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand> =>
        services.AddCommandHandler<TCommand, THandler>(static _ => { });

    /// <summary>
    /// Registers a handler for a command that does not return a response, using the specified options.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to handle.</typeparam>
    /// <typeparam name="THandler">The type of command handler to register.</typeparam>
    /// <param name="services">The service collection to add the handler to.</param>
    /// <param name="configure">The delegate that configures the handler registration.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddCommandHandler<TCommand,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        Action<DispatcherOptions> configure)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand> =>
        AddHandler<ICommandHandler<TCommand>, THandler>(services, configure);

    /// <summary>
    /// Registers a notification handler.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to handle.</typeparam>
    /// <typeparam name="THandler">The type of notification handler to register.</typeparam>
    /// <param name="services">The service collection to add the handler to.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddNotificationHandler<TNotification,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services)
        where TNotification : INotification
        where THandler : class, INotificationHandler<TNotification> =>
        services.AddNotificationHandler<TNotification, THandler>(static _ => { });

    /// <summary>
    /// Registers a notification handler with the specified options.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to handle.</typeparam>
    /// <typeparam name="THandler">The type of notification handler to register.</typeparam>
    /// <param name="services">The service collection to add the handler to.</param>
    /// <param name="configure">The delegate that configures the handler registration.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddNotificationHandler<TNotification,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        Action<DispatcherOptions> configure)
        where TNotification : INotification
        where THandler : class, INotificationHandler<TNotification> =>
        AddHandler<INotificationHandler<TNotification>, THandler>(services, configure);

    /// <summary>
    /// Registers a pipeline behavior for a request and response type.
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response produced by the pipeline.</typeparam>
    /// <typeparam name="TBehavior">The type of pipeline behavior to register.</typeparam>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddPipelineBehavior<TRequest, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TBehavior>(
        this IServiceCollection services)
        where TRequest : IRequest
        where TBehavior : class, IPipelineBehavior<TRequest, TResponse> =>
        services.AddPipelineBehavior<TRequest, TResponse, TBehavior>(static _ => { });

    /// <summary>
    /// Registers a pipeline behavior for a request and response type with the specified options.
    /// </summary>
    /// <typeparam name="TRequest">The type of request handled by the pipeline.</typeparam>
    /// <typeparam name="TResponse">The type of response produced by the pipeline.</typeparam>
    /// <typeparam name="TBehavior">The type of pipeline behavior to register.</typeparam>
    /// <param name="services">The service collection to add the behavior to.</param>
    /// <param name="configure">The delegate that configures the behavior registration.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.
    /// </exception>
    public static IServiceCollection AddPipelineBehavior<TRequest, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TBehavior>(
        this IServiceCollection services,
        Action<DispatcherOptions> configure)
        where TRequest : IRequest
        where TBehavior : class, IPipelineBehavior<TRequest, TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);

        // Registering the same behavior twice would run it twice in every pipeline it applies to.
        // The first registration wins, so the outermost-first behavior order is preserved.
        if (IsServiceRegistered(services, typeof(IPipelineBehavior<TRequest, TResponse>), typeof(TBehavior)))
        {
            return services;
        }

        var lifetime = GetLifetime(configure);
        services.Add(ServiceDescriptor.Describe(
            typeof(IPipelineBehavior<TRequest, TResponse>),
            typeof(TBehavior),
            lifetime));

        return services;
    }

    private static IServiceCollection AddHandler<TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        IServiceCollection services,
        Action<DispatcherOptions> configure)
        where TService : class
        where THandler : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        if (IsServiceRegistered(services, typeof(TService), typeof(THandler)))
        {
            return services;
        }

        var lifetime = GetLifetime(configure);
        services.Add(ServiceDescriptor.Describe(
            typeof(TService),
            typeof(THandler),
            lifetime));

        return services;
    }

    private static ServiceLifetime GetLifetime(Action<DispatcherOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DispatcherOptions();
        configure(options);
        return options.ServiceLifetime;
    }

    private static bool IsServiceRegistered(
        IServiceCollection services,
        Type serviceType,
        Type implementationType) =>
        services.Any(descriptor =>
            descriptor.ServiceType == serviceType &&
            (descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType()) ==
            implementationType);
}