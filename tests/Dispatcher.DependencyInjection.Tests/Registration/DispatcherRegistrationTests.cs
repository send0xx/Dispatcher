using Dispatcher.DependencyInjection.Tests.TestSupport;
using Dispatcher.TestSupport.AdditionalHandlers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Registration;

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

        Assert.Equal("Hello, Ada",
            await dispatcher.QueryAsync(new GreetingQuery("Ada"), TestContext.Current.CancellationToken));
        Assert.Equal(5, await dispatcher.ExecuteAsync(new SumCommand(2, 3), TestContext.Current.CancellationToken));
        await dispatcher.ExecuteAsync(new RecordCommand("typed"), TestContext.Current.CancellationToken);
        await dispatcher.PublishAsync(new SomethingHappened(), TestContext.Current.CancellationToken);

        var state = scope.ServiceProvider.GetRequiredService<TestState>();
        Assert.Equal("typed", state.Recorded);
        Assert.Equal(["handler", "sum-handler", "notification-a", "notification-b"], state.Events);
    }

    [Fact]
    public async Task Typed_registration_can_compose_a_polymorphic_message_route()
    {
        var services = new ServiceCollection();
        services
            .AddDispatcher()
            .AddQueryHandler<BaseGreetingQuery, string, BaseGreetingQueryHandler>()
            .AddDispatcherMessage<DerivedGreetingQuery>();
        services.AddScoped<TestState>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new DerivedGreetingQuery("Ada"), TestContext.Current.CancellationToken);

        Assert.Equal("Base hello, Ada", result);
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
    public async Task Repeating_a_typed_registration_registers_the_handler_once()
    {
        var services = new ServiceCollection();
        services
            .AddDispatcher()
            .AddQueryHandler<GreetingQuery, string, GreetingQueryHandler>()
            .AddQueryHandler<GreetingQuery, string, GreetingQueryHandler>()
            .AddNotificationHandler<SomethingHappened, ANotificationHandler>()
            .AddNotificationHandler<SomethingHappened, ANotificationHandler>();
        services.AddScoped<TestState>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        Assert.Equal("Hello, Ada",
            await dispatcher.QueryAsync(new GreetingQuery("Ada"), TestContext.Current.CancellationToken));
        await dispatcher.PublishAsync(new SomethingHappened(), TestContext.Current.CancellationToken);

        Assert.Equal(["handler", "notification-a"], scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Typed_registration_and_assembly_scanning_may_overlap_for_one_handler()
    {
        var services = new ServiceCollection();
        services
            .AddDispatcher()
            .AddQueryHandler<GreetingQuery, string, GreetingQueryHandler>();
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        services.AddScoped<TestState>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Ada"), TestContext.Current.CancellationToken);

        Assert.Equal("Hello, Ada", result);
    }

    [Fact]
    public async Task Assembly_scanning_and_typed_registration_may_overlap_for_one_handler()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        services
            .AddNotificationHandler<SomethingHappened, ANotificationHandler>()
            .AddDispatcher();
        services.AddScoped<TestState>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new SomethingHappened(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["notification-a", "notification-b"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Overlapping_notification_registration_invokes_each_handler_once()
    {
        var services = new ServiceCollection();
        services
            .AddDispatcher()
            .AddNotificationHandler<SomethingHappened, ANotificationHandler>();
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        services.AddScoped<TestState>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new SomethingHappened(), TestContext.Current.CancellationToken);

        var state = scope.ServiceProvider.GetRequiredService<TestState>();
        Assert.Equal(["notification-a", "notification-b"], state.Events);
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
        Assert.Equal(2, services.Count);
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
    public async Task Existing_handler_service_is_discovered_without_metadata()
    {
        var services = new ServiceCollection();
        services.AddScoped<IQueryHandler<GreetingQuery, string>, GreetingQueryHandler>();
        services.AddDispatcher();
        services.AddScoped<TestState>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Ada"), TestContext.Current.CancellationToken);

        Assert.Equal("Hello, Ada", result);
        Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IQueryHandler<GreetingQuery, string>));
    }

    [Fact]
    public async Task Scanning_does_not_duplicate_a_handler_already_registered_as_an_instance()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddSingleton(state);
        services.AddSingleton<INotificationHandler<SomethingHappened>>(new ANotificationHandler(state));
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        services.AddDispatcher();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new SomethingHappened(), TestContext.Current.CancellationToken);

        Assert.Equal(["notification-a", "notification-b"], state.Events);
    }

    [Fact]
    public void Open_notification_registration_is_idempotent_and_uses_self_registration()
    {
        var services = new ServiceCollection();

        services.AddNotificationHandler(typeof(FirstOpenNotificationHandler<>));
        services.AddNotificationHandler(typeof(FirstOpenNotificationHandler<>));

        var service = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(FirstOpenNotificationHandler<>));
        Assert.Equal(typeof(FirstOpenNotificationHandler<>), service.ImplementationType);
        Assert.False(service.IsKeyedService);
        Assert.Single(services);
    }

    [Fact]
    public void Open_notification_registration_uses_dispatcher_options()
    {
        var services = new ServiceCollection();

        services.AddNotificationHandler(typeof(FirstOpenNotificationHandler<>), options =>
            options.ServiceLifetime = ServiceLifetime.Singleton);

        Assert.Equal(
            ServiceLifetime.Singleton,
            Assert.Single(
                services,
                descriptor => descriptor.ServiceType == typeof(FirstOpenNotificationHandler<>)).Lifetime);
    }

    [Fact]
    public void Rejects_noncanonical_open_notification_handler()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() =>
            services.AddNotificationHandler(typeof(Dictionary<,>)));
    }

    [Fact]
    public void Registration_methods_reject_a_null_service_collection()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddDispatcher());
        Assert.Throws<ArgumentNullException>(() => services.AddDispatcherHandlers<TestAssemblyMarker>());
        Assert.Throws<ArgumentNullException>(() =>
            services.AddDispatcherHandlers(typeof(TestAssemblyMarker).Assembly));
        Assert.Throws<ArgumentNullException>(() =>
            services.AddQueryHandler<GreetingQuery, string, GreetingQueryHandler>());
        Assert.Throws<ArgumentNullException>(() =>
            services.AddCommandHandler<RecordCommand, RecordCommandHandler>());
        Assert.Throws<ArgumentNullException>(() =>
            services.AddNotificationHandler<SomethingHappened, ANotificationHandler>());
        Assert.Throws<ArgumentNullException>(() => services.AddPipelineBehavior<FirstGreetingBehavior>());
        Assert.Throws<ArgumentNullException>(() =>
            services.AddPipelineBehavior<GreetingQuery, string, FirstGreetingBehavior>());
    }

    [Fact]
    public void Registration_methods_reject_a_null_options_delegate()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddDispatcher(configure: null!));
        Assert.Throws<ArgumentNullException>(() =>
            services.AddDispatcherHandlers<TestAssemblyMarker>(configure: null!));
        Assert.Throws<ArgumentNullException>(() =>
            services.AddQueryHandler<GreetingQuery, string, GreetingQueryHandler>(configure: null!));
        Assert.Throws<ArgumentNullException>(() =>
            services.AddPipelineBehavior<FirstGreetingBehavior>(configure: null!));
    }

    [Fact]
    public void Assembly_and_handler_type_arguments_reject_null()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() => services.AddDispatcherHandlers(assembly: null!));
        Assert.Throws<ArgumentNullException>(() => services.AddNotificationHandler(handlerType: null!));
    }
}