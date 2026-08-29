using Dispatcher.DependencyInjection.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Registration;

public sealed class ServiceCollectionHandlerRegistrationTests
{
    [Fact]
    public async Task Direct_handler_registrations_route_when_dispatcher_and_scanning_are_added_after_them()
    {
        var services = new ServiceCollection();
        services.AddScoped<IQueryHandler<GreetingQuery, string>, GreetingQueryHandler>();
        services.AddScoped<ICommandHandler<RecordCommand>, RecordCommandHandler>();
        services.AddScoped<ICommandHandler<SumCommand, int>, SumCommandHandler>();
        services.AddScoped<INotificationHandler<SomethingHappened>, ANotificationHandler>();
        services.AddScoped(typeof(INotificationHandler<>), typeof(OrderEvents<>));
        services.AddScoped<TestState>();
        services.AddDispatcher();
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var queryResponse = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Ada"), TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new RecordCommand("executed"), TestContext.Current.CancellationToken);
        var commandResponse = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new SumCommand(2, 3), TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new SomethingHappened(), TestContext.Current.CancellationToken);
        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new OrderCreated(), TestContext.Current.CancellationToken);

        var state = scope.ServiceProvider.GetRequiredService<TestState>();
        Assert.Equal("Hello, Ada", queryResponse);
        Assert.Equal(5, commandResponse);
        Assert.Equal("executed", state.Recorded);
        Assert.Equal(
            [
                "handler",
                "sum-handler",
                "notification-a",
                "notification-b",
                "open-OrderCreated",
                "order-created"
            ],
            state.Events);
    }

    [Fact]
    public async Task A_query_handler_added_to_the_service_collection_routes()
    {
        var services = CreateServices();
        services.AddScoped<IQueryHandler<GreetingQuery, string>, GreetingQueryHandler>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var response = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Ada"), TestContext.Current.CancellationToken);

        Assert.Equal("Hello, Ada", response);
    }

    [Fact]
    public async Task A_command_handler_added_to_the_service_collection_routes()
    {
        var services = CreateServices();
        services.AddScoped<ICommandHandler<RecordCommand>, RecordCommandHandler>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new RecordCommand("executed"), TestContext.Current.CancellationToken);

        Assert.Equal("executed", scope.ServiceProvider.GetRequiredService<TestState>().Recorded);
    }

    [Fact]
    public async Task A_base_command_handler_added_to_the_service_collection_handles_a_derived_command()
    {
        var services = CreateServices();
        services.AddScoped<ICommandHandler<BaseRecordCommand>, BaseRecordCommandHandler>();
        services.AddDispatcherMessage<DerivedRecordCommand>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new DerivedRecordCommand("executed"), TestContext.Current.CancellationToken);

        var state = scope.ServiceProvider.GetRequiredService<TestState>();
        Assert.Equal(["base-command"], state.Events);
        Assert.Equal("executed", state.Recorded);
    }

    [Fact]
    public async Task A_command_handler_with_a_response_added_to_the_service_collection_routes()
    {
        var services = CreateServices();
        services.AddScoped<ICommandHandler<SumCommand, int>, SumCommandHandler>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var response = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new SumCommand(2, 3), TestContext.Current.CancellationToken);

        Assert.Equal(5, response);
    }

    [Fact]
    public async Task A_notification_handler_added_to_the_service_collection_routes()
    {
        var services = CreateServices();
        services.AddScoped<INotificationHandler<SomethingHappened>, ANotificationHandler>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new SomethingHappened(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["notification-a"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task An_open_notification_handler_added_to_the_service_collection_runs_after_the_closed_handler()
    {
        var services = CreateServices();
        services.AddScoped<INotificationHandler<OrderCreated>, OrderCreatedEventHandler>();
        services.AddScoped(typeof(INotificationHandler<>), typeof(OrderEvents<>));
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new OrderCreated(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["order-created", "open-OrderCreated"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task An_open_notification_handler_added_to_the_service_collection_observes_a_known_notification()
    {
        var services = CreateServices();
        services.AddScoped(typeof(INotificationHandler<>), typeof(OrderEvents<>));
        services.AddDispatcherMessage<OrderShipped>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new OrderShipped(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["open-OrderShipped"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task An_open_notification_handler_added_to_the_service_collection_closes_over_the_handled_type()
    {
        var services = CreateServices();
        services.AddScoped<INotificationHandler<OrderEvent>, OrderEventHandler>();
        services.AddScoped(typeof(INotificationHandler<>), typeof(OrderEvents<>));
        services.AddDispatcherMessage<OrderShipped>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new OrderShipped(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["order-event", "open-OrderEvent"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task A_mapped_open_notification_handler_runs_once_when_scanning_follows_the_mapping()
    {
        var services = CreateServices();
        services.AddScoped(typeof(INotificationHandler<>), typeof(OrderEvents<>));
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new OrderCreated(), TestContext.Current.CancellationToken);

        // A mapped handler is served through IEnumerable<INotificationHandler<OrderCreated>>, so it
        // runs in the order Microsoft DI resolves it rather than after the closed handler.
        Assert.Equal(
            ["open-OrderCreated", "order-created"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task A_mapped_open_notification_handler_runs_once_when_the_mapping_follows_scanning()
    {
        var services = CreateServices();
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        services.AddScoped(typeof(INotificationHandler<>), typeof(OrderEvents<>));
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new OrderCreated(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["order-created", "open-OrderCreated"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task A_mapped_open_notification_handler_runs_once_when_it_is_also_added_by_type()
    {
        var services = CreateServices();
        services.AddScoped(typeof(INotificationHandler<>), typeof(OrderEvents<>));
        services.AddNotificationHandler(typeof(OrderEvents<>));
        services.AddDispatcherMessage<OrderShipped>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new OrderShipped(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["open-OrderShipped"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddDispatcher();
        services.AddScoped<TestState>();
        return services;
    }
}