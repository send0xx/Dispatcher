using Dispatcher.SourceGeneration;
using Microsoft.Extensions.DependencyInjection;

[assembly: GenerateDispatcherHandlers("AddGeneratedIntegrationHandlers")]
[assembly: GenerateDispatcher("AddGeneratedIntegrationDispatcher")]

namespace Dispatcher.SourceGeneration.Tests.TestSupport;

public sealed class GeneratedTestState
{
    public TaskCompletionSource<string> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<string> Events { get; } = [];
}

internal sealed class GeneratedIntegrationHost : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private GeneratedIntegrationHost(ServiceProvider provider)
    {
        _provider = provider;
        Scope = provider.CreateAsyncScope();
    }

    internal AsyncServiceScope Scope { get; }

    internal IDispatcher Dispatcher => Scope.ServiceProvider.GetRequiredService<IDispatcher>();

    internal GeneratedTestState State => Scope.ServiceProvider.GetRequiredService<GeneratedTestState>();

    internal static GeneratedIntegrationHost Create(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<GeneratedTestState>();
        services
            .AddGeneratedIntegrationHandlers()
            .AddGeneratedIntegrationDispatcher();
        configure?.Invoke(services);

        return new GeneratedIntegrationHost(services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        }));
    }

    public async ValueTask DisposeAsync()
    {
        await Scope.DisposeAsync();
        await _provider.DisposeAsync();
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