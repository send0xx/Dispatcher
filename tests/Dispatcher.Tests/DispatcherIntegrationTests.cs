using System.Collections.Frozen;
using System.Reflection;
using Dispatcher.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.Tests;

public sealed class DispatcherIntegrationTests
{
    [Fact]
    public async Task Dispatches_query_and_both_command_shapes()
    {
        await using var provider = CreateProvider();
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
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();

        var received = await scope.ServiceProvider
            .GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new TokenQuery(), cancellation.Token);

        Assert.Equal(cancellation.Token, received);
    }

    [Fact]
    public async Task Runs_first_registered_behavior_outermost()
    {
        var services = CreateServices();
        services.AddPipelineBehavior<FirstGreetingBehavior>();
        services.AddPipelineBehavior<SecondGreetingBehavior>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
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
        var services = CreateServices();
        services.AddPipelineBehavior<ShortCircuitSumBehavior>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new SumCommand(10, 20));

        Assert.Equal(42, result);
        Assert.DoesNotContain("sum-handler", scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Resultless_command_uses_the_same_typed_pipeline_behavior()
    {
        var services = CreateServices();
        services.AddPipelineBehavior<RecordCommandBehavior>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
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
        var services = CreateServices();
        services.AddPipelineBehavior<PassthroughGreetingBehavior>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        var first = dispatcher.QueryAsync(new GreetingQuery("Ada")).AsTask();
        var second = dispatcher.QueryAsync(new GreetingQuery("Grace")).AsTask();

        Assert.Equal(["Hello, Ada", "Hello, Grace"], await Task.WhenAll(first, second));
    }

    [Fact]
    public async Task Transient_behavior_is_resolved_for_every_dispatch()
    {
        var services = CreateServices();
        services.AddPipelineBehavior<TransientGreetingBehavior>(ServiceLifetime.Transient);
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();

        await dispatcher.QueryAsync(new GreetingQuery("Ada"));
        await dispatcher.QueryAsync(new GreetingQuery("Grace"));

        Assert.Equal(2, scope.ServiceProvider.GetRequiredService<TestState>().BehaviorInstances);
    }

    [Fact]
    public async Task Behavior_registered_directly_in_microsoft_di_is_executed()
    {
        var services = CreateServices();
        services.AddScoped<IPipelineBehavior<GreetingQuery, string>, FirstGreetingBehavior>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GreetingQuery("Ada"));

        Assert.Equal(
            ["first-before", "handler", "first-after"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Publishes_notifications_sequentially_in_registration_order()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationPublisher>()
            .PublishAsync(new SomethingHappened());

        Assert.Equal(["notification-a", "notification-b"],
            scope.ServiceProvider.GetRequiredService<TestState>().Events);
    }

    [Fact]
    public async Task Notification_without_handlers_is_no_op()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationPublisher>()
            .PublishAsync(new UnhandledNotification());
    }

    [Fact]
    public async Task Missing_handler_throws_descriptive_exception()
    {
        await using var provider = CreateProvider();
        await using var scope = provider.CreateAsyncScope();

        var exception = await Assert.ThrowsAsync<HandlerNotFoundException>(() =>
            scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
                .QueryAsync(new MissingQuery()).AsTask());

        Assert.Equal(typeof(MissingQuery), exception.MessageType);
    }

    [Fact]
    public void Duplicate_request_handlers_are_rejected_when_registry_is_built()
    {
        var registrations = new[]
        {
            new HandlerRegistration(typeof(GreetingQuery), typeof(string), HandlerKind.Query, typeof(GreetingQueryHandler)),
            new HandlerRegistration(typeof(GreetingQuery), typeof(string), HandlerKind.Query, typeof(AlternativeGreetingHandler))
        };

        Assert.Throws<DuplicateHandlerException>(() => DispatcherRegistry.Create(registrations));
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
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();

        var aggregate = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        Assert.Same(aggregate, scope.ServiceProvider.GetRequiredService<IQueryDispatcher>());
        Assert.Same(aggregate, scope.ServiceProvider.GetRequiredService<ICommandDispatcher>());
        Assert.Same(aggregate, scope.ServiceProvider.GetRequiredService<INotificationPublisher>());
        Assert.Equal(2, scope.ServiceProvider.GetServices<INotificationHandler<SomethingHappened>>().Count());
    }

    [Fact]
    public async Task Registry_uses_frozen_dictionaries()
    {
        await using var provider = CreateProvider();
        var registry = provider.GetRequiredService<DispatcherRegistry>();
        var properties = typeof(DispatcherRegistry).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.All(properties, property =>
            Assert.Equal(typeof(FrozenDictionary<,>), property.PropertyType.GetGenericTypeDefinition()));
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

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddDispatcher();
        services.AddDispatcherHandlers<TestAssemblyMarker>();
        services.AddScoped<TestState>();
        return services;
    }

    private static ServiceProvider CreateProvider() =>
        CreateServices().BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

    private sealed class AlternativeGreetingHandler;
}

public sealed class TestAssemblyMarker;

public sealed class TestState
{
    public string? Recorded { get; set; }
    public List<string> Events { get; } = [];
    public int BehaviorInstances { get; set; }
}

public sealed record GreetingQuery(string Name) : IQuery<string>;
internal sealed class GreetingQueryHandler(TestState state) : IQueryHandler<GreetingQuery, string>
{
    public ValueTask<string> HandleAsync(GreetingQuery query, CancellationToken cancellationToken)
    {
        state.Events.Add("handler");
        return ValueTask.FromResult($"Hello, {query.Name}");
    }
}

public sealed record TokenQuery : IQuery<CancellationToken>;
internal sealed class TokenQueryHandler : IQueryHandler<TokenQuery, CancellationToken>
{
    public ValueTask<CancellationToken> HandleAsync(TokenQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(cancellationToken);
}

public sealed record SumCommand(int Left, int Right) : ICommand<int>;
internal sealed class SumCommandHandler(TestState state) : ICommandHandler<SumCommand, int>
{
    public ValueTask<int> HandleAsync(SumCommand command, CancellationToken cancellationToken)
    {
        state.Events.Add("sum-handler");
        return ValueTask.FromResult(command.Left + command.Right);
    }
}

public sealed record RecordCommand(string Value) : ICommand;
internal sealed class RecordCommandHandler(TestState state) : ICommandHandler<RecordCommand>
{
    public ValueTask HandleAsync(RecordCommand command, CancellationToken cancellationToken)
    {
        state.Recorded = command.Value;
        return ValueTask.CompletedTask;
    }
}

public sealed record SomethingHappened : INotification;
internal sealed class ANotificationHandler(TestState state) : INotificationHandler<SomethingHappened>
{
    public ValueTask HandleAsync(SomethingHappened notification, CancellationToken cancellationToken)
    {
        state.Events.Add("notification-a");
        return ValueTask.CompletedTask;
    }
}

internal sealed class BNotificationHandler(TestState state) : INotificationHandler<SomethingHappened>
{
    public ValueTask HandleAsync(SomethingHappened notification, CancellationToken cancellationToken)
    {
        state.Events.Add("notification-b");
        return ValueTask.CompletedTask;
    }
}

public sealed record UnhandledNotification : INotification;
public sealed record MissingQuery : IQuery<int>;

internal sealed class FirstGreetingBehavior(TestState state) : IPipelineBehavior<GreetingQuery, string>
{
    public async ValueTask<string> HandleAsync(
        GreetingQuery query,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        state.Events.Add("first-before");
        var result = await next(cancellationToken);
        state.Events.Add("first-after");
        return result;
    }
}

internal sealed class SecondGreetingBehavior(TestState state) : IPipelineBehavior<GreetingQuery, string>
{
    public async ValueTask<string> HandleAsync(
        GreetingQuery query,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        state.Events.Add("second-before");
        var result = await next(cancellationToken);
        state.Events.Add("second-after");
        return result;
    }
}

internal sealed class ShortCircuitSumBehavior : IPipelineBehavior<SumCommand, int>
{
    public ValueTask<int> HandleAsync(
        SumCommand command,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken) => ValueTask.FromResult(42);
}

internal sealed class RecordCommandBehavior(TestState state) : IPipelineBehavior<RecordCommand, Unit>
{
    public async ValueTask<Unit> HandleAsync(
        RecordCommand command,
        RequestHandlerDelegate<Unit> next,
        CancellationToken cancellationToken)
    {
        state.Events.Add("record-before");
        var result = await next(cancellationToken);
        state.Events.Add("record-after");
        return result;
    }
}

internal sealed class PassthroughGreetingBehavior : IPipelineBehavior<GreetingQuery, string>
{
    public ValueTask<string> HandleAsync(
        GreetingQuery query,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}

internal sealed class TransientGreetingBehavior : IPipelineBehavior<GreetingQuery, string>
{
    public TransientGreetingBehavior(TestState state)
    {
        state.BehaviorInstances++;
    }

    public ValueTask<string> HandleAsync(
        GreetingQuery query,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}