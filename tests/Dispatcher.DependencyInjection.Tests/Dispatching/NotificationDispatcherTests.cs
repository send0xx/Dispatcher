using Dispatcher.DependencyInjection.Tests.TestSupport;
using Dispatcher.TestSupport.AdditionalHandlers;
using Dispatcher.TestSupport.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Dispatching;

public sealed class NotificationDispatcherTests
{
    [Fact]
    public async Task Publishes_notifications_sequentially_in_registration_order()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new SomethingHappened(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["notification-a", "notification-b"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Overriding_an_open_generic_handler_adds_a_closed_route_without_replacing_it()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new StockAdjusted(), TestContext.Current.CancellationToken);

        // StockAdjustedHandler derives from InventoryEventHandler<StockAdjusted>, so its override is the
        // closed route. The open handler is a separate registration and still runs its own instance.
        Assert.Equal(
            ["stock-adjusted-override", "inventory-base-StockAdjusted"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Notification_without_handlers_is_no_op()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new UnhandledNotification(), TestContext.Current.CancellationToken);

        Assert.Empty(scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Routes_derived_notification_to_all_handlers_for_selected_base_type()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new UserUpdatedEvent(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["domain-a", "domain-b"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Exact_notification_handlers_take_precedence_over_base_handlers()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new UserCreatedEvent(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["user-created"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Open_handlers_observe_a_known_notification_without_a_closed_handler()
    {
        var services = new ServiceCollection();
        services.AddDispatcher();
        services.AddDispatcherHandlers<AdditionalHandlerAssemblyMarker>();
        services.AddSingleton<OpenNotificationRecorder>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new OpenOnlyNotification(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["open-a-OpenOnlyNotification", "open-b-OpenOnlyNotification"],
            scope.ServiceProvider.GetRequiredService<OpenNotificationRecorder>().Events);
        Assert.Empty(scope.ServiceProvider.GetServices<INotificationHandler<OpenOnlyNotification>>());
    }

    [Fact]
    public async Task Open_handlers_run_after_the_selected_polymorphic_closed_route()
    {
        var services = new ServiceCollection();
        services.AddDispatcher();
        services.AddDispatcherHandlers<AdditionalHandlerAssemblyMarker>();
        services.AddSingleton<OpenNotificationRecorder>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new DerivedSharedNotification(), TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                "closed-base",
                "open-a-DerivedSharedNotification",
                "open-b-DerivedSharedNotification",
                "open-shared-DerivedSharedNotification"
            ],
            scope.ServiceProvider.GetRequiredService<OpenNotificationRecorder>().Events);
    }

    [Fact]
    public async Task Exact_closed_route_wins_while_open_handlers_use_the_concrete_type()
    {
        var services = new ServiceCollection();
        services.AddDispatcher();
        services.AddDispatcherHandlers<AdditionalHandlerAssemblyMarker>();
        services.AddSingleton<OpenNotificationRecorder>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new ExactSharedNotification(), TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                "closed-exact",
                "open-a-ExactSharedNotification",
                "open-b-ExactSharedNotification",
                "open-shared-ExactSharedNotification"
            ],
            scope.ServiceProvider.GetRequiredService<OpenNotificationRecorder>().Events);
    }
}