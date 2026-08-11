using Dispatcher.DependencyInjection;
using Dispatcher;
using Dispatcher.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.Tests.Registration;

public sealed class DispatcherRegistrationTests
{
    [Fact]
    public async Task Typed_registration_dispatches_every_message_shape()
    {
        var services = new ServiceCollection();
        services
            .AddDispatcher()
            .AddQueryHandler<GreetingQuery, string, GreetingQueryHandler>()
            .AddCommandHandler<SumCommand, int, SumCommandHandler>()
            .AddCommandHandler<RecordCommand, RecordCommandHandler>()
            .AddNotificationHandler<SomethingHappened, ANotificationHandler>()
            .AddNotificationHandler<SomethingHappened, BNotificationHandler>();
        services.AddScoped<TestState>();

        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        Assert.Equal("Hello, Ada", await dispatcher.QueryAsync(new GreetingQuery("Ada")));
        Assert.Equal(5, await dispatcher.ExecuteAsync(new SumCommand(2, 3)));
        await dispatcher.ExecuteAsync(new RecordCommand("typed"));
        await dispatcher.PublishAsync(new SomethingHappened());

        var state = scope.ServiceProvider.GetRequiredService<TestState>();
        Assert.Equal("typed", state.Recorded);
        Assert.Equal(["handler", "sum-handler", "notification-a", "notification-b"], state.Events);
    }

    [Fact]
    public async Task Registration_is_idempotent_and_all_contracts_share_scoped_instance()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        services.AddDispatcher();
        services.AddDispatcher();
        services.AddScoped<TestState>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var aggregate = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        Assert.Same(aggregate, scope.ServiceProvider.GetRequiredService<IQueryDispatcher>());
        Assert.Same(aggregate, scope.ServiceProvider.GetRequiredService<ICommandDispatcher>());
        Assert.Same(aggregate, scope.ServiceProvider.GetRequiredService<INotificationDispatcher>());
        Assert.Equal(2, scope.ServiceProvider.GetServices<INotificationHandler<SomethingHappened>>().Count());
    }

    [Fact]
    public void Dispatcher_lifetime_is_scoped_by_default()
    {
        var services = new ServiceCollection();

        services.AddDispatcher();

        Assert.All(
            services.Where(descriptor =>
                descriptor.ServiceType == typeof(Dispatcher) ||
                descriptor.ServiceType == typeof(IDispatcher) ||
                descriptor.ServiceType == typeof(IQueryDispatcher) ||
                descriptor.ServiceType == typeof(ICommandDispatcher) ||
                descriptor.ServiceType == typeof(INotificationDispatcher)),
            descriptor => Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime));
    }

    [Fact]
    public void Dispatcher_can_be_registered_as_transient()
    {
        var services = new ServiceCollection();
        services.AddDispatcher(options =>
            options.ServiceLifetime = ServiceLifetime.Transient);
        using var provider = TestServices.BuildProvider(services);

        var first = provider.GetRequiredService<IDispatcher>();
        var second = provider.GetRequiredService<IDispatcher>();

        Assert.NotSame(first, second);
        Assert.All(
            services.Where(descriptor =>
                descriptor.ServiceType == typeof(Dispatcher) ||
                descriptor.ServiceType == typeof(IDispatcher) ||
                descriptor.ServiceType == typeof(IQueryDispatcher) ||
                descriptor.ServiceType == typeof(ICommandDispatcher) ||
                descriptor.ServiceType == typeof(INotificationDispatcher)),
            descriptor => Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime));
    }

    [Fact]
    public void Dispatcher_rejects_singleton_lifetime()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddDispatcher(options =>
                options.ServiceLifetime = ServiceLifetime.Singleton));
    }

    [Fact]
    public void Handler_registration_uses_dispatcher_options()
    {
        var services = new ServiceCollection();

        services.AddDispatcherHandlers<TestAssemblyMarker>(options =>
            options.ServiceLifetime = ServiceLifetime.Singleton);

        Assert.Equal(
            ServiceLifetime.Singleton,
            Assert.Single(
                services,
                descriptor => descriptor.ServiceType == typeof(IQueryHandler<GreetingQuery, string>))
                .Lifetime);
        Assert.Equal(
            ServiceLifetime.Singleton,
            Assert.Single(
                services,
                descriptor => descriptor.ServiceType == typeof(ICommandHandler<SumCommand, int>))
                .Lifetime);
    }

    [Fact]
    public void AddDispatcher_does_not_scan_handlers()
    {
        var services = new ServiceCollection();
        services.AddDispatcher();
        using var provider = services.BuildServiceProvider();

        Assert.Empty(provider.GetServices<IQueryHandler<GreetingQuery, string>>());
    }

    [Fact]
    public void Rejects_invalid_pipeline_behavior_type()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddPipelineBehavior(typeof(string)));
    }

    [Fact]
    public void Rejects_partially_closed_open_pipeline_behavior_type()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddPipelineBehavior(typeof(PartiallyClosedGreetingBehavior<>)));
    }

    [Fact]
    public void Rejects_open_pipeline_behavior_with_fixed_response_type()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddPipelineBehavior(typeof(FixedResponseBehavior<>)));
    }

    [Fact]
    public void Rejects_reordered_open_pipeline_behavior_type_parameters()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddPipelineBehavior(typeof(ReorderedBehavior<,>)));
    }

    [Fact]
    public void Registers_canonical_open_pipeline_behavior_type()
    {
        var services = new ServiceCollection();

        services.AddPipelineBehavior(typeof(OpenPassthroughBehavior<,>));

        var descriptor = Assert.Single(services);
        Assert.Equal(typeof(IPipelineBehavior<,>), descriptor.ServiceType);
        Assert.Equal(typeof(OpenPassthroughBehavior<,>), descriptor.ImplementationType);
    }

    [Fact]
    public void Canonical_open_pipeline_behavior_can_limit_requests_with_constraints()
    {
        var services = new ServiceCollection();
        services.AddPipelineBehavior(typeof(TransactionalBehavior<,>));
        using var provider = TestServices.BuildProvider(services);
        using var scope = provider.CreateScope();

        Assert.Empty(scope.ServiceProvider.GetServices<IPipelineBehavior<GreetingQuery, string>>());
        Assert.Single(scope.ServiceProvider.GetServices<IPipelineBehavior<TransactionalQuery, string>>());
    }

    [Fact]
    public void Typed_handler_registration_is_idempotent()
    {
        var services = new ServiceCollection();

        services.AddQueryHandler<GreetingQuery, string, GreetingQueryHandler>();
        services.AddQueryHandler<GreetingQuery, string, GreetingQueryHandler>();
        services.AddNotificationHandler<SomethingHappened, ANotificationHandler>();
        services.AddNotificationHandler<SomethingHappened, ANotificationHandler>();

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IQueryHandler<GreetingQuery, string>));
        Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(INotificationHandler<SomethingHappened>));
        Assert.Equal(
            2,
            services.Count(descriptor => descriptor.ServiceType == typeof(HandlerRegistration)));
    }

    [Fact]
    public void Typed_handler_registration_uses_dispatcher_options()
    {
        var services = new ServiceCollection();

        services.AddQueryHandler<GreetingQuery, string, GreetingQueryHandler>(options =>
            options.ServiceLifetime = ServiceLifetime.Singleton);

        Assert.Equal(
            ServiceLifetime.Singleton,
            Assert.Single(
                services,
                descriptor => descriptor.ServiceType == typeof(IQueryHandler<GreetingQuery, string>))
                .Lifetime);
    }

    [Fact]
    public async Task Typed_registration_adds_metadata_for_an_existing_handler_service()
    {
        var services = new ServiceCollection();
        services.AddScoped<IQueryHandler<GreetingQuery, string>, GreetingQueryHandler>();
        services.AddQueryHandler<GreetingQuery, string, GreetingQueryHandler>();
        services.AddDispatcher();
        services.AddScoped<TestState>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Ada"));

        Assert.Equal("Hello, Ada", result);
        Assert.Single(provider.GetServices<HandlerRegistration>());
    }
}
