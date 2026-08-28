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
    public async Task Constrained_open_notification_handler_observes_only_notifications_it_can_close_over()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers<HandlerAssemblyMarker>();
        services.AddNotificationHandler(typeof(RestrictedOpenNotificationHandler<>));
        services.AddDispatcher();
        services.AddSingleton<OpenNotificationRecorder>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        await dispatcher.PublishAsync(new RestrictedNotification(), TestContext.Current.CancellationToken);
        await dispatcher.PublishAsync(new OpenOnlyNotification(), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["open-restricted-RestrictedNotification"],
            scope.ServiceProvider.GetRequiredService<OpenNotificationRecorder>().Events);
    }

    [Fact]
    public async Task Constraint_precheck_preserves_special_and_self_referencing_constraints()
    {
        var services = new ServiceCollection();
        services.AddDispatcherMessage<StructNotification>();
        services.AddDispatcherMessage<ClassNotification>();
        services.AddDispatcherMessage<ComparableNotification>();
        services.AddDispatcherMessage<NonComparableNotification>();
        services.AddNotificationHandler(typeof(StructNotificationHandler<>));
        services.AddNotificationHandler(typeof(ComparableNotificationHandler<>));
        services.AddDispatcher();
        services.AddSingleton<ConstraintRecorder>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();

        await dispatcher.PublishAsync(new StructNotification(), TestContext.Current.CancellationToken);
        await dispatcher.PublishAsync(new ClassNotification(), TestContext.Current.CancellationToken);
        await dispatcher.PublishAsync(new ComparableNotification(), TestContext.Current.CancellationToken);
        await dispatcher.PublishAsync(new NonComparableNotification(), TestContext.Current.CancellationToken);

        Assert.Equal(
            [typeof(StructNotification), typeof(ComparableNotification)],
            scope.ServiceProvider.GetRequiredService<ConstraintRecorder>().Notifications);
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

    [Fact]
    public async Task Closed_handler_registered_after_a_scan_routes_messages_discovered_by_that_scan()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers<HandlerAssemblyMarker>();
        services.AddQueryHandler<LaterBaseQuery, string, LaterBaseQueryHandler>();
        services.AddDispatcher();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new LaterDerivedQuery("typed"), TestContext.Current.CancellationToken);

        Assert.Equal("Handled later typed", result);
    }

    private readonly record struct StructNotification : INotification;

    private sealed record ClassNotification : INotification;

    private sealed record ComparableNotification : INotification, IComparable<ComparableNotification>
    {
        public int CompareTo(ComparableNotification? other) => 0;
    }

    private sealed record NonComparableNotification : INotification;

    private sealed class StructNotificationHandler<TNotification>(ConstraintRecorder recorder)
        : INotificationHandler<TNotification>
        where TNotification : struct, INotification
    {
        public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken)
        {
            recorder.Notifications.Add(typeof(TNotification));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ComparableNotificationHandler<TNotification>(ConstraintRecorder recorder)
        : INotificationHandler<TNotification>
        where TNotification : INotification, IComparable<TNotification>
    {
        public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken)
        {
            recorder.Notifications.Add(typeof(TNotification));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ConstraintRecorder
    {
        internal List<Type> Notifications { get; } = [];
    }
}