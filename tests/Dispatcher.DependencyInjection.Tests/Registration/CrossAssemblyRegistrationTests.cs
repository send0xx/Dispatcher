using Dispatcher.DependencyInjection;
using Dispatcher.TestSupport.AdditionalHandlers;
using Dispatcher.TestSupport.ConflictingHandlers;
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
            .QueryAsync(new SharedDerivedQuery("across assemblies"), TestContext.Current.CancellationToken);

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
            .QueryAsync(new LaterDerivedQuery("module"), TestContext.Current.CancellationToken);

        Assert.Equal("Handled later module", result);
    }

    [Fact]
    public async Task Scanned_and_explicitly_registered_handlers_for_one_query_are_rejected()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers<HandlerAssemblyMarker>();
        services.AddQueryHandler<SharedBaseQuery, string, ConflictingSharedBaseQueryHandler>();
        services.AddDispatcher();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();

        var exception = Assert.Throws<DuplicateHandlerException>(
            scope.ServiceProvider.GetRequiredService<IQueryDispatcher>);

        Assert.Equal(typeof(SharedBaseQuery), exception.MessageType);
    }

    [Fact]
    public async Task Scanned_handlers_for_two_equally_specific_interfaces_are_rejected()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers<ConflictingHandlerAssemblyMarker>();
        services.AddDispatcher();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();

        var exception = Assert.Throws<AmbiguousHandlerException>(
            scope.ServiceProvider.GetRequiredService<IQueryDispatcher>);

        Assert.Equal(typeof(AmbiguousScanQuery), exception.MessageType);
        Assert.Equal(
            [typeof(IAlphaQuery), typeof(IBetaQuery)],
            exception.CandidateMessageTypes);
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
            .PublishAsync(new OpenOnlyNotification(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["open-a-OpenOnlyNotification"],
            scope.ServiceProvider.GetRequiredService<OpenNotificationRecorder>().Events);
    }
}