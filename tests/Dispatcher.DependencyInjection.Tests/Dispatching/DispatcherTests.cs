using Dispatcher.DependencyInjection.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Dispatching;

public sealed class DispatcherTests
{
    [Fact]
    public async Task Dispatches_query_and_both_command_shapes()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var queryResult = await dispatcher.QueryAsync(new GreetingQuery("Ada"), TestContext.Current.CancellationToken);
        var commandResult = await dispatcher.ExecuteAsync(new SumCommand(2, 3), TestContext.Current.CancellationToken);
        await dispatcher.ExecuteAsync(new RecordCommand("done"), TestContext.Current.CancellationToken);

        Assert.Equal("Hello, Ada", queryResult);
        Assert.Equal(5, commandResult);
        Assert.Equal("done", scope.ServiceProvider.GetRequiredService<TestState>().Recorded);
    }

    [Fact]
    public async Task Passes_cancellation_token_to_handler()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();

        var received = await scope.ServiceProvider
            .GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new TokenQuery(), cancellation.Token);

        Assert.Equal(cancellation.Token, received);
    }

    [Fact]
    public async Task Missing_handler_throws_descriptive_exception()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        var exception = await Assert.ThrowsAsync<HandlerNotFoundException>(() =>
            scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
                .QueryAsync(new MissingQuery(), TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(typeof(MissingQuery), exception.MessageType);
    }

    [Fact]
    public async Task Missing_handler_throws_for_both_command_overloads()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var resultless = await Assert.ThrowsAsync<HandlerNotFoundException>(async () =>
            await dispatcher.ExecuteAsync(new MissingCommand(), TestContext.Current.CancellationToken));
        var withResponse = await Assert.ThrowsAsync<HandlerNotFoundException>(async () =>
            await dispatcher.ExecuteAsync(new MissingResponseCommand(), TestContext.Current.CancellationToken));

        Assert.Equal(typeof(MissingCommand), resultless.MessageType);
        Assert.Equal(typeof(MissingResponseCommand), withResponse.MessageType);
    }

    [Fact]
    public async Task Missing_resultless_command_handler_throws_synchronously()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var exception = Assert.Throws<HandlerNotFoundException>(() =>
        {
            _ = dispatcher.ExecuteAsync(new MissingCommand(), TestContext.Current.CancellationToken);
        });

        Assert.Equal(typeof(MissingCommand), exception.MessageType);
    }

    [Fact]
    public async Task Dispatching_a_message_through_the_wrong_shape_throws()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var query = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await dispatcher.QueryAsync(new CommandShapedQuery(), TestContext.Current.CancellationToken));
        var command = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await dispatcher.ExecuteAsync(new QueryShapedCommand(), TestContext.Current.CancellationToken));
        var resultless = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await dispatcher.ExecuteAsync(new QueryShapedResultlessCommand(), TestContext.Current.CancellationToken));

        Assert.All(
            [query, command, resultless],
            exception => Assert.Contains("does not match message type", exception.Message, StringComparison.Ordinal));
        Assert.Contains(typeof(CommandShapedQuery).FullName!, query.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(QueryShapedCommand).FullName!, command.Message, StringComparison.Ordinal);
        Assert.Contains(typeof(QueryShapedResultlessCommand).FullName!, resultless.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Routes_derived_queries_and_commands_to_base_handlers()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var queryResult = await dispatcher.QueryAsync(new DerivedGreetingQuery("Ada"), TestContext.Current.CancellationToken);
        var commandResult = await dispatcher.ExecuteAsync(new DerivedSumCommand(2, 3), TestContext.Current.CancellationToken);
        await dispatcher.ExecuteAsync(new DerivedRecordCommand("polymorphic"), TestContext.Current.CancellationToken);

        Assert.Equal("Base hello, Ada", queryResult);
        Assert.Equal(5, commandResult);
        var state = scope.ServiceProvider.GetRequiredService<TestState>();
        Assert.Equal("polymorphic", state.Recorded);
        Assert.Equal(
            ["base-query", "base-response-command", "base-command"],
            state.Events);
    }

    [Fact]
    public async Task Exact_query_handler_takes_precedence_over_base_handler()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new SpecificGreetingQuery("Ada"), TestContext.Current.CancellationToken);

        Assert.Equal("Specific hello, Ada", result);
        Assert.Equal(
            ["specific-query"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Selects_the_most_specific_handled_interface_when_no_handler_is_exact()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new CarQuery(), TestContext.Current.CancellationToken);

        Assert.Equal("car", result);
        Assert.Equal(
            ["car-query"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }
}