using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Extensions.Microsoft.DependencyInjection;

/// <summary>
/// Provides typed Dispatcher registration methods for Microsoft dependency injection.
/// </summary>
public static class TypedDispatcherServiceCollectionExtensions
{
    /// <summary>
    /// Registers a query handler.
    /// </summary>
    /// <typeparam name="TQuery">The query type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The query handler implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The handler lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddQueryHandler<TQuery, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TQuery : IQuery<TResponse>
        where THandler : class, IQueryHandler<TQuery, TResponse>
    {
        return AddHandler<IQueryHandler<TQuery, TResponse>, THandler>(
            services,
            QueryHandlerRegistration.Create<TQuery, TResponse, THandler>(),
            lifetime);
    }

    /// <summary>
    /// Registers a result-bearing command handler.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The command handler implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The handler lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCommandHandler<TCommand, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TCommand : ICommand<TResponse>
        where THandler : class, ICommandHandler<TCommand, TResponse>
    {
        return AddHandler<ICommandHandler<TCommand, TResponse>, THandler>(
            services,
            CommandWithResponseHandlerRegistration.Create<TCommand, TResponse, THandler>(),
            lifetime);
    }

    /// <summary>
    /// Registers a resultless command handler.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <typeparam name="THandler">The command handler implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The handler lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCommandHandler<TCommand,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TCommand : ICommand
        where THandler : class, ICommandHandler<TCommand>
    {
        return AddHandler<ICommandHandler<TCommand>, THandler>(
            services,
            CommandHandlerRegistration.Create<TCommand, THandler>(),
            lifetime);
    }

    /// <summary>
    /// Registers a notification handler.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <typeparam name="THandler">The notification handler implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The handler lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNotificationHandler<TNotification,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TNotification : INotification
        where THandler : class, INotificationHandler<TNotification>
    {
        return AddHandler<INotificationHandler<TNotification>, THandler>(
            services,
            NotificationHandlerRegistration.Create<TNotification, THandler>(),
            lifetime);
    }

    private static IServiceCollection AddHandler<TService,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        IServiceCollection services,
        HandlerRegistration registration,
        ServiceLifetime lifetime)
        where TService : class
        where THandler : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Add(ServiceDescriptor.Describe(
            typeof(TService),
            typeof(THandler),
            lifetime));
        services.AddSingleton(registration);

        return services;
    }

    /// <summary>
    /// Registers a pipeline behavior for a request and response type.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="TBehavior">The pipeline behavior implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The behavior lifetime.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPipelineBehavior<TRequest, TResponse,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TBehavior>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TRequest : IRequest
        where TBehavior : class, IPipelineBehavior<TRequest, TResponse>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Add(ServiceDescriptor.Describe(
            typeof(IPipelineBehavior<TRequest, TResponse>),
            typeof(TBehavior),
            lifetime));

        return services;
    }
}