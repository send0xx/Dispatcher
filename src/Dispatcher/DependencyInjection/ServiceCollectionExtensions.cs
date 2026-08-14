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
        AddQueryHandler<TQuery, TResponse, THandler>(services, static _ => { });

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
        AddHandler<IQueryHandler<TQuery, TResponse>, THandler>(
            services,
            new QueryHandlerRegistration(typeof(TQuery), typeof(TResponse), typeof(THandler)),
            GetLifetime(configure));

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
        AddCommandHandler<TCommand, TResponse, THandler>(services, static _ => { });

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
        AddHandler<ICommandHandler<TCommand, TResponse>, THandler>(
            services,
            new CommandWithResponseHandlerRegistration(typeof(TCommand), typeof(TResponse), typeof(THandler)),
            GetLifetime(configure));

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
        AddCommandHandler<TCommand, THandler>(services, static _ => { });

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
        AddHandler<ICommandHandler<TCommand>, THandler>(
            services,
            new CommandHandlerRegistration(typeof(TCommand), typeof(THandler)),
            GetLifetime(configure));

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
        AddNotificationHandler<TNotification, THandler>(services, static _ => { });

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
        AddHandler<INotificationHandler<TNotification>, THandler>(
            services,
            new NotificationHandlerRegistration(typeof(TNotification), typeof(THandler)),
            GetLifetime(configure));

    /// <summary>
    /// Registers an open generic notification handler.
    /// </summary>
    /// <param name="services">The service collection to add the handler to.</param>
    /// <param name="handlerType">The open generic notification handler type to register.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="handlerType"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="handlerType"/> is not a canonical open generic notification handler.
    /// </exception>
    public static IServiceCollection AddNotificationHandler(
        this IServiceCollection services,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)]
        Type handlerType) =>
        AddNotificationHandler(services, handlerType, static _ => { });

    /// <summary>
    /// Registers an open generic notification handler with the specified options.
    /// </summary>
    /// <param name="services">The service collection to add the handler to.</param>
    /// <param name="handlerType">The open generic notification handler type to register.</param>
    /// <param name="configure">The delegate that configures the handler registration.</param>
    /// <returns>The same service collection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/>, <paramref name="handlerType"/>, or <paramref name="configure"/> is
    /// <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="handlerType"/> is not a canonical open generic notification handler.
    /// </exception>
    public static IServiceCollection AddNotificationHandler(
        this IServiceCollection services,
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)]
        Type handlerType,
        Action<DispatcherOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(handlerType);

        var messageType = GetOpenNotificationTypeParameter(handlerType);
        var lifetime = GetLifetime(configure);
        if (!services.Any(descriptor =>
                descriptor.ServiceType == handlerType &&
                descriptor.ImplementationType == handlerType))
        {
            services.Add(ServiceDescriptor.Describe(handlerType, handlerType, lifetime));
        }

        var registration = new NotificationHandlerRegistration(messageType, handlerType);
        if (!services.Any(descriptor =>
                descriptor.ServiceType == typeof(HandlerRegistration) &&
                descriptor.ImplementationInstance is HandlerRegistration existing &&
                IsSameRegistration(existing, registration)))
        {
            services.AddSingleton<HandlerRegistration>(registration);
        }

        return services;
    }

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
        AddPipelineBehavior<TRequest, TResponse, TBehavior>(services, static _ => { });

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

        services.Add(ServiceDescriptor.Describe(
            typeof(IPipelineBehavior<TRequest, TResponse>),
            typeof(TBehavior),
            GetLifetime(configure)));

        return services;
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

        if (!services.Any(descriptor =>
                descriptor.ServiceType == typeof(TService) &&
                descriptor.ImplementationType == typeof(THandler)))
        {
            services.Add(ServiceDescriptor.Describe(
                typeof(TService),
                typeof(THandler),
                lifetime));
        }

        if (!services.Any(descriptor =>
                descriptor.ServiceType == typeof(HandlerRegistration) &&
                descriptor.ImplementationInstance is HandlerRegistration existing &&
                IsSameRegistration(existing, registration)))
        {
            services.AddSingleton(registration);
        }

        return services;
    }

    private static ServiceLifetime GetLifetime(Action<DispatcherOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DispatcherOptions();
        configure(options);
        return options.ServiceLifetime;
    }

    private static Type GetOpenNotificationTypeParameter(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)]
        Type handlerType)
    {
        var handlerInterfaces = handlerType.GetInterfaces()
            .Where(@interface => @interface.IsGenericType &&
                @interface.GetGenericTypeDefinition() is var definition &&
                (definition == typeof(IQueryHandler<,>) ||
                 definition == typeof(ICommandHandler<,>) ||
                 definition == typeof(ICommandHandler<>) ||
                 definition == typeof(INotificationHandler<>)))
            .ToArray();
        if (!handlerType.IsGenericTypeDefinition ||
            !handlerType.IsClass ||
            handlerType.IsAbstract ||
            handlerType.GetGenericArguments() is not [var typeParameter] ||
            handlerInterfaces is not [var handlerInterface] ||
            handlerInterface.GetGenericTypeDefinition() != typeof(INotificationHandler<>) ||
            handlerInterface.GetGenericArguments()[0] != typeParameter ||
            handlerType.GetConstructors().Length == 0)
        {
            throw new ArgumentException(
                $"Handler '{handlerType.FullName}' must be a non-abstract open generic class with one type " +
                "parameter, implement INotificationHandler<TNotification> using that parameter directly, " +
                "and expose a public constructor.",
                nameof(handlerType));
        }

        return typeParameter;
    }

    private static bool IsSameRegistration(
        HandlerRegistration existing,
        HandlerRegistration registration)
    {
        if (existing.GetType() != registration.GetType() ||
            existing.MessageType != registration.MessageType ||
            existing.HandlerType != registration.HandlerType)
        {
            return false;
        }

        return (existing, registration) switch
        {
            (QueryHandlerRegistration first, QueryHandlerRegistration second) =>
                first.ResponseType == second.ResponseType,
            (CommandWithResponseHandlerRegistration first, CommandWithResponseHandlerRegistration second) =>
                first.ResponseType == second.ResponseType,
            _ => true
        };
    }
}