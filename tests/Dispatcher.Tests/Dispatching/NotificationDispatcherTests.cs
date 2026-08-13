using Dispatcher.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.Tests.Dispatching;

public sealed class NotificationDispatcherTests
{
    [Fact]
    public async Task Publishes_notifications_sequentially_in_registration_order()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new SomethingHappened());

        Assert.Equal(
            ["notification-a", "notification-b"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Notification_without_handlers_is_no_op()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new UnhandledNotification());
    }

    [Fact]
    public async Task Routes_derived_notification_to_all_handlers_for_selected_base_type()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new UserUpdatedEvent());

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
            .PublishAsync(new UserCreatedEvent());

        Assert.Equal(
            ["user-created"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }
}