using Dispatcher.DependencyInjection;
using Dispatcher.Extensions.Microsoft.DependencyInjection;
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
    }
}