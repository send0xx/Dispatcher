using Dispatcher.DependencyInjection;
using Dispatcher.TestSupport.AdditionalHandlers;
using Dispatcher.TestSupport.Contracts;
using Dispatcher.TestSupport.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Registration;

public sealed class CrossAssemblyRegistrationTests
{
    [Fact]
    public async Task Handler_scan_discovers_derived_messages_from_the_shared_contracts_assembly()
    {
        var services = new ServiceCollection();
        services
            .AddDispatcherHandlers<HandlerAssemblyMarker>()
            .AddDispatcher();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new SharedDerivedQuery("across assemblies"));

        Assert.Equal("Handled across assemblies", result);
    }

    [Fact]
    public async Task Later_handler_scan_adds_routes_from_an_already_scanned_contracts_assembly()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers<HandlerAssemblyMarker>();
        services.AddDispatcherHandlers<AdditionalHandlerAssemblyMarker>();
        services.AddDispatcher();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new LaterDerivedQuery("module"));

        Assert.Equal("Handled later module", result);
    }

    [Fact]
    public async Task Open_notification_handler_registered_after_a_scan_observes_that_scan_s_messages()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers<HandlerAssemblyMarker>();
        services.AddNotificationHandler(typeof(FirstOpenNotificationHandler<>));
        services.AddDispatcher();
        services.AddSingleton<OpenNotificationRecorder>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new OpenOnlyNotification());

        Assert.Equal(
            ["open-a-OpenOnlyNotification"],
            scope.ServiceProvider.GetRequiredService<OpenNotificationRecorder>().Events);
    }
}