using Dispatcher.DependencyInjection;
using Dispatcher.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.Tests.Pipeline;

public sealed class PipelineBehaviorTests
{
    [Fact]
    public async Task Runs_first_registered_behavior_outermost()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<FirstGreetingBehavior>();
        services.AddPipelineBehavior<SecondGreetingBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Grace"));

        Assert.Equal(
            ["first-before", "second-before", "handler", "second-after", "first-after"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Behavior_can_short_circuit_handler()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<ShortCircuitSumBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new SumCommand(10, 20));

        Assert.Equal(42, result);
        Assert.DoesNotContain("sum-handler", scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Resultless_command_uses_the_same_typed_pipeline_behavior()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<RecordCommandBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new RecordCommand("through-pipeline"));

        var state = scope.ServiceProvider.GetRequiredService<TestState>();
        Assert.Equal("through-pipeline", state.Recorded);
        Assert.Equal(["record-before", "record-after"], state.Events);
    }

    [Fact]
    public async Task Pipeline_is_safe_for_concurrent_requests_in_the_same_scope()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<PassthroughGreetingBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var first = dispatcher.QueryAsync(new GreetingQuery("Ada")).AsTask();
        var second = dispatcher.QueryAsync(new GreetingQuery("Grace")).AsTask();

        Assert.Equal(["Hello, Ada", "Hello, Grace"], await Task.WhenAll(first, second));
    }

    [Fact]
    public async Task Transient_behavior_is_resolved_for_every_dispatch()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<TransientGreetingBehavior>(ServiceLifetime.Transient);
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        await dispatcher.QueryAsync(new GreetingQuery("Ada"));
        await dispatcher.QueryAsync(new GreetingQuery("Grace"));

        Assert.Equal(2, scope.ServiceProvider.GetRequiredService<TestState>().BehaviorInstances);
    }

    [Fact]
    public async Task Behavior_registered_directly_in_microsoft_di_is_executed()
    {
        var services = TestServices.CreateServices();
        services.AddScoped<IPipelineBehavior<GreetingQuery, string>, FirstGreetingBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Ada"));

        Assert.Equal(
            ["first-before", "handler", "first-after"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }
}