using System.Collections.Concurrent;
using System.Diagnostics;
using Dispatcher.SourceGeneration;
using Dispatcher.TestSupport.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[assembly: GenerateDispatcherHandlers("AddGeneratedIntegrationHandlers")]
[assembly: GenerateDispatcher("AddGeneratedIntegrationDispatcher")]

namespace Dispatcher.SourceGeneration.Tests.Integration;

public sealed class GeneratedTelemetryTests
{
    [Fact]
    public async Task Async_dispatch_restores_the_parent_activity()
    {
        var instrumentationName = "Dispatcher.SourceGeneration.Tests." + Guid.NewGuid();
        using var capture = new GeneratedActivityCapture(instrumentationName);
        var services = new ServiceCollection();
        services.AddSingleton<GeneratedTestState>();
        services
            .AddGeneratedIntegrationHandlers()
            .AddGeneratedIntegrationDispatcher(options =>
            {
                options.Telemetry.EnableTracing = true;
                options.Telemetry.ActivitySourceName = instrumentationName;
            });
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        var state = scope.ServiceProvider.GetRequiredService<GeneratedTestState>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IQueryDispatcher>();
        using var parent = new Activity("parent").Start();

        var response = dispatcher.QueryAsync(new GeneratedDelayedQuery(), TestContext.Current.CancellationToken);

        Assert.False(response.IsCompleted);
        Assert.Same(parent, Activity.Current);

        state.Completion.SetResult("completed");

        Assert.Equal("completed", await response);
        Assert.Same(parent, Activity.Current);
        Assert.Same(parent, Assert.Single(capture.Activities).Parent);
    }

    [Fact]
    public async Task Dispatches_derived_messages_to_the_most_specific_handlers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<GeneratedTestState>();
        services
            .AddGeneratedIntegrationHandlers()
            .AddGeneratedIntegrationDispatcher();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var queryResult = await dispatcher.QueryAsync(new GeneratedDerivedQuery("Ada"), TestContext.Current.CancellationToken);
        var commandResult = await dispatcher.ExecuteAsync(new GeneratedDerivedCommand(2, 3), TestContext.Current.CancellationToken);
        await dispatcher.PublishAsync(new GeneratedUserUpdatedEvent(), TestContext.Current.CancellationToken);

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
            scope.ServiceProvider.GetRequiredService<GeneratedTestState>().Events);
    }

    [Fact]
    public async Task Dispatches_derived_resultless_command_to_base_handler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<GeneratedTestState>();
        services
            .AddGeneratedIntegrationHandlers()
            .AddGeneratedIntegrationDispatcher();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>()
            .ExecuteAsync(new GeneratedDerivedResultlessCommand("value"), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["base-resultless-command"],
            scope.ServiceProvider.GetRequiredService<GeneratedTestState>().Events);
    }

    [Fact]
    public async Task Resultless_command_without_a_handler_throws_synchronously()
    {
        var services = new ServiceCollection();
        services.AddSingleton<GeneratedTestState>();
        services
            .AddGeneratedIntegrationHandlers()
            .AddGeneratedIntegrationDispatcher();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        var exception = Assert.Throws<HandlerNotFoundException>(() =>
        {
            _ = dispatcher.ExecuteAsync(new UnhandledCommand(), TestContext.Current.CancellationToken);
        });

        Assert.Equal(typeof(UnhandledCommand), exception.MessageType);
    }

    [Fact]
    public async Task Polymorphic_route_uses_pipeline_behavior_for_handled_base_type()
    {
        var services = new ServiceCollection();
        services.AddSingleton<GeneratedTestState>();
        services
            .AddGeneratedIntegrationHandlers()
            .AddGeneratedIntegrationDispatcher()
            .AddPipelineBehavior(typeof(GeneratedRecordingBehavior<,>));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new GeneratedDerivedQuery("Ada"), TestContext.Current.CancellationToken);

        Assert.Equal("Base hello, Ada", result);
        Assert.Equal(
            ["behavior-before-GeneratedBaseQuery", "base-query", "behavior-after-GeneratedBaseQuery"],
            scope.ServiceProvider.GetRequiredService<GeneratedTestState>().Events);
    }

    [Fact]
    public async Task Exact_handlers_take_precedence_over_base_handlers()
    {
        var services = new ServiceCollection();
        services.AddSingleton<GeneratedTestState>();
        services
            .AddGeneratedIntegrationHandlers()
            .AddGeneratedIntegrationDispatcher();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var result = await dispatcher.QueryAsync(new GeneratedSpecificQuery("Ada"), TestContext.Current.CancellationToken);
        await dispatcher.PublishAsync(new GeneratedUserCreatedEvent(), TestContext.Current.CancellationToken);

        Assert.Equal("Specific hello, Ada", result);
        Assert.Equal(
            [
                "specific-query",
                "user-created",
                "open-GeneratedUserCreatedEvent",
                "domain-open-GeneratedUserCreatedEvent"
            ],
            scope.ServiceProvider.GetRequiredService<GeneratedTestState>().Events);
    }

    [Fact]
    public async Task Open_handler_observes_a_known_notification_without_a_closed_handler()
    {
        var services = new ServiceCollection();
        services.AddSingleton<GeneratedTestState>();
        services
            .AddGeneratedIntegrationHandlers()
            .AddGeneratedIntegrationDispatcher();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<INotificationDispatcher>()
            .PublishAsync(new GeneratedStandaloneEvent(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["open-GeneratedStandaloneEvent"],
            scope.ServiceProvider.GetRequiredService<GeneratedTestState>().Events);
        Assert.Empty(scope.ServiceProvider.GetServices<INotificationHandler<GeneratedStandaloneEvent>>());
    }
}

public sealed record GeneratedDelayedQuery : IQuery<string>;

public sealed class GeneratedDelayedQueryHandler(GeneratedTestState state)
    : IQueryHandler<GeneratedDelayedQuery, string>
{
    public ValueTask<string> HandleAsync(
        GeneratedDelayedQuery query,
        CancellationToken cancellationToken) =>
        new(state.Completion.Task);
}

public sealed class GeneratedTestState
{
    public TaskCompletionSource<string> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public List<string> Events { get; } = [];
}

public abstract record GeneratedBaseQuery(string Name) : IQuery<string>;
public sealed record GeneratedDerivedQuery(string Name) : GeneratedBaseQuery(Name);
public sealed record GeneratedSpecificQuery(string Name) : GeneratedBaseQuery(Name);

public sealed class GeneratedBaseQueryHandler(GeneratedTestState state)
    : IQueryHandler<GeneratedBaseQuery, string>
{
    public ValueTask<string> HandleAsync(
        GeneratedBaseQuery query,
        CancellationToken cancellationToken)
    {
        state.Events.Add("base-query");
        return ValueTask.FromResult($"Base hello, {query.Name}");
    }
}

public sealed class GeneratedSpecificQueryHandler(GeneratedTestState state)
    : IQueryHandler<GeneratedSpecificQuery, string>
{
    public ValueTask<string> HandleAsync(
        GeneratedSpecificQuery query,
        CancellationToken cancellationToken)
    {
        state.Events.Add("specific-query");
        return ValueTask.FromResult($"Specific hello, {query.Name}");
    }
}

public abstract record GeneratedBaseCommand(int Left, int Right) : ICommand<int>;
public sealed record GeneratedDerivedCommand(int Left, int Right) : GeneratedBaseCommand(Left, Right);

public sealed class GeneratedBaseCommandHandler(GeneratedTestState state)
    : ICommandHandler<GeneratedBaseCommand, int>
{
    public ValueTask<int> HandleAsync(
        GeneratedBaseCommand command,
        CancellationToken cancellationToken)
    {
        state.Events.Add("base-command");
        return ValueTask.FromResult(command.Left + command.Right);
    }
}

public abstract record GeneratedBaseResultlessCommand(string Value) : ICommand;
public sealed record GeneratedDerivedResultlessCommand(string Value) : GeneratedBaseResultlessCommand(Value);

public sealed class GeneratedBaseResultlessCommandHandler(GeneratedTestState state)
    : ICommandHandler<GeneratedBaseResultlessCommand>
{
    public ValueTask HandleAsync(
        GeneratedBaseResultlessCommand command,
        CancellationToken cancellationToken)
    {
        state.Events.Add("base-resultless-command");
        return ValueTask.CompletedTask;
    }
}

public sealed class GeneratedRecordingBehavior<TRequest, TResponse>(GeneratedTestState state)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest
{
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        state.Events.Add($"behavior-before-{typeof(TRequest).Name}");
        var result = await next(cancellationToken);
        state.Events.Add($"behavior-after-{typeof(TRequest).Name}");
        return result;
    }
}

public interface IGeneratedAuditedNotification : INotification;

public abstract record GeneratedDomainEvent : IGeneratedAuditedNotification;
public sealed record GeneratedUserUpdatedEvent : GeneratedDomainEvent;
public sealed record GeneratedUserCreatedEvent : GeneratedDomainEvent;
public sealed record GeneratedStandaloneEvent : IGeneratedAuditedNotification;

public sealed class GeneratedAuditNotificationHandler<TNotification>(GeneratedTestState state)
    : INotificationHandler<TNotification>
    where TNotification : IGeneratedAuditedNotification
{
    public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        state.Events.Add("open-" + typeof(TNotification).Name);
        return ValueTask.CompletedTask;
    }
}

public sealed class GeneratedDomainAuditNotificationHandler<TNotification>(GeneratedTestState state)
    : INotificationHandler<TNotification>
    where TNotification : GeneratedDomainEvent
{
    public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken)
    {
        state.Events.Add("domain-open-" + typeof(TNotification).Name);
        return ValueTask.CompletedTask;
    }
}

public sealed class GeneratedFirstDomainEventHandler(GeneratedTestState state)
    : INotificationHandler<GeneratedDomainEvent>
{
    public ValueTask HandleAsync(
        GeneratedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        state.Events.Add("domain-a");
        return ValueTask.CompletedTask;
    }
}

public sealed class GeneratedSecondDomainEventHandler(GeneratedTestState state)
    : INotificationHandler<GeneratedDomainEvent>
{
    public ValueTask HandleAsync(
        GeneratedDomainEvent notification,
        CancellationToken cancellationToken)
    {
        state.Events.Add("domain-b");
        return ValueTask.CompletedTask;
    }
}

public sealed class GeneratedUserCreatedEventHandler(GeneratedTestState state)
    : INotificationHandler<GeneratedUserCreatedEvent>
{
    public ValueTask HandleAsync(
        GeneratedUserCreatedEvent notification,
        CancellationToken cancellationToken)
    {
        state.Events.Add("user-created");
        return ValueTask.CompletedTask;
    }
}

internal sealed class GeneratedActivityCapture : IDisposable
{
    private readonly ActivityListener _listener;

    internal GeneratedActivityCapture(string activitySourceName)
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == activitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => Activities.Enqueue(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    internal ConcurrentQueue<Activity> Activities { get; } = new();

    public void Dispose() => _listener.Dispose();
}