using Dispatcher.SourceGeneration.Tests.TestSupport;
using Dispatcher.TestSupport.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.SourceGeneration.Tests.Integration;

public sealed class GeneratedDispatchTests
{
    [Fact]
    public async Task Dispatches_derived_messages_to_the_most_specific_handlers()
    {
        await using var host = GeneratedIntegrationHost.Create();

        var queryResult = await host.Dispatcher.QueryAsync(
            new GeneratedDerivedQuery("Ada"),
            TestContext.Current.CancellationToken);
        var commandResult = await host.Dispatcher.ExecuteAsync(
            new GeneratedDerivedCommand(2, 3),
            TestContext.Current.CancellationToken);
        await host.Dispatcher.PublishAsync(
            new GeneratedUserUpdatedEvent(),
            TestContext.Current.CancellationToken);

        Assert.Equal("Base hello, Ada", queryResult);
        Assert.Equal(5, commandResult);
        Assert.Equal(
            [
                "base-query",
                "base-command",
                "domain-a",
                "domain-b",
                "open-GeneratedUserUpdatedEvent",
                "domain-open-GeneratedUserUpdatedEvent"
            ],
            host.State.Events);
    }

    [Fact]
    public async Task Dispatches_derived_resultless_command_to_base_handler()
    {
        await using var host = GeneratedIntegrationHost.Create();

        await host.Dispatcher.ExecuteAsync(
            new GeneratedDerivedResultlessCommand("value"),
            TestContext.Current.CancellationToken);

        Assert.Equal(["base-resultless-command"], host.State.Events);
    }

    /// <remarks>
    /// The command is declared in a referenced contracts assembly, because the generator rejects a
    /// request with no handler at compile time and so one cannot be declared here.
    /// </remarks>
    [Fact]
    public async Task Resultless_command_without_a_handler_throws_synchronously()
    {
        await using var host = GeneratedIntegrationHost.Create();

        var exception = Assert.Throws<HandlerNotFoundException>(() =>
        {
            _ = host.Dispatcher.ExecuteAsync(new UnhandledCommand(), TestContext.Current.CancellationToken);
        });

        Assert.Equal(typeof(UnhandledCommand), exception.MessageType);
    }

    [Fact]
    public async Task Polymorphic_route_uses_pipeline_behavior_for_handled_base_type()
    {
        await using var host = GeneratedIntegrationHost.Create(services =>
            services.AddPipelineBehavior(typeof(GeneratedRecordingBehavior<,>)));

        var result = await host.Dispatcher.QueryAsync(
            new GeneratedDerivedQuery("Ada"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Base hello, Ada", result);
        Assert.Equal(
            ["behavior-before-GeneratedBaseQuery", "base-query", "behavior-after-GeneratedBaseQuery"],
            host.State.Events);
    }

    [Fact]
    public async Task Exact_handlers_take_precedence_over_base_handlers()
    {
        await using var host = GeneratedIntegrationHost.Create();

        var result = await host.Dispatcher.QueryAsync(
            new GeneratedSpecificQuery("Ada"),
            TestContext.Current.CancellationToken);
        await host.Dispatcher.PublishAsync(
            new GeneratedUserCreatedEvent(),
            TestContext.Current.CancellationToken);

        Assert.Equal("Specific hello, Ada", result);
        Assert.Equal(
            [
                "specific-query",
                "user-created",
                "open-GeneratedUserCreatedEvent",
                "domain-open-GeneratedUserCreatedEvent"
            ],
            host.State.Events);
    }

    [Fact]
    public async Task Open_handler_observes_a_known_notification_without_a_closed_handler()
    {
        await using var host = GeneratedIntegrationHost.Create();

        await host.Dispatcher.PublishAsync(
            new GeneratedStandaloneEvent(),
            TestContext.Current.CancellationToken);

        Assert.Equal(["open-GeneratedStandaloneEvent"], host.State.Events);
        Assert.Empty(host.Scope.ServiceProvider.GetServices<INotificationHandler<GeneratedStandaloneEvent>>());
    }
}