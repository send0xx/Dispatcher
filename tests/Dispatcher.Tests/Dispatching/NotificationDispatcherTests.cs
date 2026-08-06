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
}