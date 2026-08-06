using Dispatcher.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.Tests.Dispatching;

public sealed class DispatcherTests
{
    [Fact]
    public async Task Dispatches_query_and_both_command_shapes()
    {
        await using var provider = TestServices.CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var queryResult = await dispatcher.QueryAsync(new GreetingQuery("Ada"));
        var commandResult = await dispatcher.ExecuteAsync(new SumCommand(2, 3));
        await dispatcher.ExecuteAsync(new RecordCommand("done"));

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
                .QueryAsync(new MissingQuery()).AsTask());

        Assert.Equal(typeof(MissingQuery), exception.MessageType);
    }
}