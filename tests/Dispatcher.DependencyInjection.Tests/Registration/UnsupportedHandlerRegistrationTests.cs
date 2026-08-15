using Dispatcher.DependencyInjection;
using Dispatcher.TestSupport.UnsupportedHandlers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Registration;

public sealed class UnsupportedHandlerRegistrationTests
{
    [Fact]
    public void Scanning_reports_every_unsupported_handler_in_one_exception()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<UnsupportedHandlerException>(() =>
            services.AddDispatcherHandlers(typeof(UnsupportedPing).Assembly));

        Assert.Equal(
            [
                typeof(ClosedHandlerWithoutPublicConstructor),
                typeof(GenericHandlerWithClosedNotification<>),
                typeof(OpenGenericQueryHandler<>),
                typeof(OpenNotificationHandlerWithoutPublicConstructor<>)
            ],
            exception.Handlers.Keys.OrderBy(handler => handler.FullName, StringComparer.Ordinal));
    }

    [Fact]
    public void Unsupported_handlers_are_reported_with_the_reason_they_cannot_be_registered()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<UnsupportedHandlerException>(() =>
            services.AddDispatcherHandlers(typeof(UnsupportedPing).Assembly));

        Assert.Contains(
            "public constructor",
            exception.Handlers[typeof(ClosedHandlerWithoutPublicConstructor)]);
        Assert.Contains(
            "public constructor",
            exception.Handlers[typeof(OpenNotificationHandlerWithoutPublicConstructor<>)]);
        Assert.Contains(
            "not a supported open generic handler",
            exception.Handlers[typeof(GenericHandlerWithClosedNotification<>)]);
        Assert.Contains(
            "not a supported open generic handler",
            exception.Handlers[typeof(OpenGenericQueryHandler<>)]);
    }

    [Fact]
    public void A_failed_scan_leaves_the_service_collection_untouched()
    {
        var services = new ServiceCollection();

        Assert.Throws<UnsupportedHandlerException>(() =>
            services.AddDispatcherHandlers(typeof(UnsupportedPing).Assembly));

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(HandlerRegistration));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(MessageRegistration));
    }
}