using Dispatcher.DependencyInjection.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Pipeline;

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
            .QueryAsync(new GreetingQuery("Grace"), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["first-before", "second-before", "handler", "second-after", "first-after"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Runs_first_registered_behavior_outermost_for_a_command_with_a_response()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<FirstSumBehavior>();
        services.AddPipelineBehavior<SecondSumBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new SumCommand(10, 20), TestContext.Current.CancellationToken);

        Assert.Equal(30, result);
        Assert.Equal(
            ["first-before", "second-before", "sum-handler", "second-after", "first-after"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    // A resultless command is adapted to Unit inside the pipeline, so it reaches the behaviors
    // through different code than a command that declares a response.
    [Fact]
    public async Task Runs_first_registered_behavior_outermost_for_a_resultless_command()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<RecordCommandBehavior>();
        services.AddPipelineBehavior<SecondRecordCommandBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new RecordCommand("ordered"), TestContext.Current.CancellationToken);

        var state = scope.ServiceProvider.GetRequiredService<TestState>();
        Assert.Equal("ordered", state.Recorded);
        Assert.Equal(
            ["record-before", "second-record-before", "second-record-after", "record-after"],
            state.Events);
    }

    [Fact]
    public async Task Registering_the_same_behavior_twice_runs_it_once()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<FirstGreetingBehavior>();
        services.AddPipelineBehavior<FirstGreetingBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Grace"), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["first-before", "handler", "first-after"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Registering_a_behavior_already_registered_as_an_instance_runs_it_once()
    {
        var state = new TestState();
        var services = new ServiceCollection();
        services.AddDispatcher();
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        services.AddSingleton(state);
        services.AddSingleton<IPipelineBehavior<GreetingQuery, string>>(new FirstGreetingBehavior(state));
        services.AddPipelineBehavior<FirstGreetingBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Grace"), TestContext.Current.CancellationToken);

        Assert.Equal(["first-before", "handler", "first-after"], state.Events);
    }

    [Fact]
    public async Task Registering_the_same_open_generic_behavior_twice_runs_it_once()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior(typeof(CommandBaseBehavior<,>));
        services.AddPipelineBehavior(typeof(CommandBaseBehavior<,>));
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new RecordCommand("once"), TestContext.Current.CancellationToken);

        Assert.Equal(["command-base"], scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Registering_the_same_typed_behavior_twice_runs_it_once()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<GreetingQuery, string, FirstGreetingBehavior>();
        services.AddPipelineBehavior<GreetingQuery, string, FirstGreetingBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Grace"), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["first-before", "handler", "first-after"],
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
            .ExecuteAsync(new SumCommand(10, 20), TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
        Assert.DoesNotContain("sum-handler", scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    // The resultless handler has no response to suppress, so the only evidence that the behavior
    // short-circuited it is that the handler never recorded the command value.
    [Fact]
    public async Task Behavior_can_short_circuit_a_resultless_command_handler()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<ShortCircuitRecordCommandBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new RecordCommand("suppressed"), TestContext.Current.CancellationToken);

        Assert.Null(scope.ServiceProvider.GetRequiredService<TestState>().Recorded);
    }

    [Fact]
    public async Task Behavior_can_replace_the_cancellation_token_passed_to_next()
    {
        using var replacement = new CancellationTokenSource();
        using var dispatch = new CancellationTokenSource();
        var services = TestServices.CreateServices();
        services.AddScoped<IPipelineBehavior<TokenQuery, CancellationToken>>(_ =>
            new TokenReplacingBehavior(replacement.Token));
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var received = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new TokenQuery(), dispatch.Token);

        Assert.Equal(replacement.Token, received);
        Assert.NotEqual(dispatch.Token, received);
    }

    [Fact]
    public async Task Behavior_can_replace_the_cancellation_token_passed_to_next_for_a_command_with_a_response()
    {
        using var replacement = new CancellationTokenSource();
        using var dispatch = new CancellationTokenSource();
        var services = TestServices.CreateServices();
        services.AddScoped<IPipelineBehavior<TokenCommand, CancellationToken>>(_ =>
            new TokenReplacingCommandBehavior(replacement.Token));
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var received = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new TokenCommand(), dispatch.Token);

        Assert.Equal(replacement.Token, received);
        Assert.NotEqual(dispatch.Token, received);
    }

    [Fact]
    public async Task Behavior_can_replace_the_cancellation_token_passed_to_next_for_a_resultless_command()
    {
        using var replacement = new CancellationTokenSource();
        using var dispatch = new CancellationTokenSource();
        var services = TestServices.CreateServices();
        services.AddScoped<IPipelineBehavior<TokenRecordingCommand, Unit>>(_ =>
            new TokenReplacingResultlessCommandBehavior(replacement.Token));
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new TokenRecordingCommand(), dispatch.Token);

        var received = scope.ServiceProvider.GetRequiredService<TestState>().ReceivedToken;
        Assert.Equal(replacement.Token, received);
        Assert.NotEqual(dispatch.Token, received);
    }

    [Fact]
    public async Task Resultless_command_uses_the_same_typed_pipeline_behavior()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<RecordCommandBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new RecordCommand("through-pipeline"), TestContext.Current.CancellationToken);

        var state = scope.ServiceProvider.GetRequiredService<TestState>();
        Assert.Equal("through-pipeline", state.Recorded);
        Assert.Equal(["record-before", "record-after"], state.Events);
    }

    [Fact]
    public async Task Command_base_behavior_applies_to_both_command_shapes_but_not_queries()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior(typeof(CommandBaseBehavior<,>));
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.QueryAsync(new GreetingQuery("Ada"), TestContext.Current.CancellationToken);
        await dispatcher.ExecuteAsync(new SumCommand(1, 2), TestContext.Current.CancellationToken);
        await dispatcher.ExecuteAsync(new RecordCommand("recorded"), TestContext.Current.CancellationToken);

        Assert.Equal(
            2,
            scope.ServiceProvider.GetRequiredService<TestState>().Events.Count(@event =>
                @event == "command-base"));
    }

    [Fact]
    public async Task Response_command_behavior_does_not_apply_to_resultless_commands_or_queries()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior(typeof(ResponseCommandBehavior<,>));
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        await dispatcher.QueryAsync(new GreetingQuery("Ada"), TestContext.Current.CancellationToken);
        await dispatcher.ExecuteAsync(new SumCommand(1, 2), TestContext.Current.CancellationToken);
        await dispatcher.ExecuteAsync(new RecordCommand("recorded"), TestContext.Current.CancellationToken);

        Assert.Equal(
            1,
            scope.ServiceProvider.GetRequiredService<TestState>().Events.Count(@event =>
                @event == "response-command"));
    }

    [Fact]
    public async Task Pipeline_is_safe_for_concurrent_requests_in_the_same_scope()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<PassthroughGreetingBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var first = dispatcher.QueryAsync(new GreetingQuery("Ada"), TestContext.Current.CancellationToken).AsTask();
        var second = dispatcher.QueryAsync(new GreetingQuery("Grace"), TestContext.Current.CancellationToken).AsTask();

        Assert.Equal(["Hello, Ada", "Hello, Grace"], await Task.WhenAll(first, second));
    }

    [Fact]
    public async Task Transient_behavior_is_resolved_for_every_dispatch()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<TransientGreetingBehavior>(options =>
            options.ServiceLifetime = ServiceLifetime.Transient);
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        await dispatcher.QueryAsync(new GreetingQuery("Ada"), TestContext.Current.CancellationToken);
        await dispatcher.QueryAsync(new GreetingQuery("Grace"), TestContext.Current.CancellationToken);

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
            .QueryAsync(new GreetingQuery("Ada"), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["first-before", "handler", "first-after"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Polymorphic_route_uses_behaviors_for_the_handled_message_type()
    {
        var services = TestServices.CreateServices();
        services.AddScoped<IPipelineBehavior<BaseGreetingQuery, string>, BaseGreetingBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new DerivedGreetingQuery("Ada"), TestContext.Current.CancellationToken);

        Assert.Equal("Base hello, Ada", result);
        Assert.Equal(
            ["base-before", "base-query", "base-after"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Polymorphic_command_route_uses_behaviors_for_the_handled_message_type()
    {
        var services = TestServices.CreateServices();
        services.AddScoped<IPipelineBehavior<BaseSumCommand, int>, BaseSumBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new DerivedSumCommand(2, 3), TestContext.Current.CancellationToken);

        Assert.Equal(5, result);
        Assert.Equal(
            ["base-response-command-before", "base-response-command", "base-response-command-after"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Polymorphic_resultless_command_route_uses_behaviors_for_the_handled_message_type()
    {
        var services = TestServices.CreateServices();
        services.AddScoped<IPipelineBehavior<BaseRecordCommand, Unit>, BaseRecordBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new DerivedRecordCommand("derived"), TestContext.Current.CancellationToken);

        var state = scope.ServiceProvider.GetRequiredService<TestState>();
        Assert.Equal("derived", state.Recorded);
        Assert.Equal(["base-command-before", "base-command", "base-command-after"], state.Events);
    }

    [Fact]
    public async Task Transient_behavior_is_resolved_for_every_command_dispatch()
    {
        var services = TestServices.CreateServices();
        services.AddPipelineBehavior<TransientSumBehavior>(options =>
            options.ServiceLifetime = ServiceLifetime.Transient);
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        await dispatcher.ExecuteAsync(new SumCommand(1, 2), TestContext.Current.CancellationToken);
        await dispatcher.ExecuteAsync(new SumCommand(3, 4), TestContext.Current.CancellationToken);

        Assert.Equal(2, scope.ServiceProvider.GetRequiredService<TestState>().BehaviorInstances);
    }

    [Fact]
    public async Task Behavior_registered_directly_in_microsoft_di_is_executed_for_a_command()
    {
        var services = TestServices.CreateServices();
        services.AddScoped<IPipelineBehavior<SumCommand, int>, FirstSumBehavior>();
        await using var provider = TestServices.BuildProvider(services);
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new SumCommand(10, 20), TestContext.Current.CancellationToken);

        Assert.Equal(30, result);
        Assert.Equal(
            ["first-before", "sum-handler", "first-after"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }
}