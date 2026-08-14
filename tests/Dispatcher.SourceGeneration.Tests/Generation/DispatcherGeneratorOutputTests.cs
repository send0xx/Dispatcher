using Dispatcher.SourceGeneration.Tests.TestSupport;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Dispatcher.SourceGeneration.Tests.Generation;

public sealed class DispatcherGeneratorOutputTests
{
    [Fact]
    public void Generates_dispatcher_for_internal_handlers()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddTestDispatcher")]

            internal sealed record TestQuery : IQuery<string>;
            internal sealed class TestQueryHandler : IQueryHandler<TestQuery, string>
            {
                public ValueTask<string> HandleAsync(TestQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("value");
            }
            """;

        var result = GeneratorTestHarness.Run(source, includeRuntimeIntegration: false);

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains(
                "internal sealed class Dispatcher",
                StringComparison.Ordinal))).ToString();
        Assert.Contains("FrozenDictionary", generated, StringComparison.Ordinal);
        Assert.Contains("QueryCore<global::TestQuery", generated, StringComparison.Ordinal);
        Assert.Contains("return handler.HandleAsync(request, cancellationToken);", generated, StringComparison.Ordinal);
        Assert.Contains("RunQueryPipeline", generated, StringComparison.Ordinal);
        Assert.Contains(result.GeneratedTrees, tree =>
            tree.ToString().Contains("AddTestDispatcher", StringComparison.Ordinal));
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generates_direct_polymorphic_routes_to_base_handlers()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddTestDispatcher")]

            internal abstract record BaseQuery : IQuery<string>;
            internal sealed record DerivedQuery : BaseQuery;
            internal sealed class BaseQueryHandler : IQueryHandler<BaseQuery, string>
            {
                public ValueTask<string> HandleAsync(BaseQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("value");
            }

            internal abstract record DomainEvent : INotification;
            internal sealed record UserCreatedEvent : DomainEvent;
            internal sealed class DomainEventHandler : INotificationHandler<DomainEvent>
            {
                public ValueTask HandleAsync(DomainEvent notification, CancellationToken cancellationToken) =>
                    ValueTask.CompletedTask;
            }
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains(
                "internal sealed class Dispatcher",
                StringComparison.Ordinal))).ToString();
        Assert.Contains(
            "[typeof(global::DerivedQuery)] = (typeof(string)",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "QueryCore<global::BaseQuery, string>((global::BaseQuery)message",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "[typeof(global::UserCreatedEvent)] = static (dispatcher, notification, token) => dispatcher.NotificationCore<global::DomainEvent>",
            generated,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "GeneratedNotificationHandlerInvoker",
            generated,
            StringComparison.Ordinal);
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generates_configurable_dispatcher_lifetime_registration()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddTestDispatcher")]
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains(
                "public static class GeneratedDispatcherServiceCollectionExtensions",
                StringComparison.Ordinal))).ToString();
        Assert.Contains(
            "Action<global::Dispatcher.DispatcherOptions> configure",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "options.ServiceLifetime",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "A singleton dispatcher would capture the root service provider.",
            generated,
            StringComparison.Ordinal);
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generates_conditional_telemetry_dispatcher_and_exception_events()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddTestDispatcher")]

            internal sealed record TestQuery : IQuery<string>;
            internal sealed class TestQueryHandler : IQueryHandler<TestQuery, string>
            {
                public ValueTask<string> HandleAsync(TestQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("value");
            }
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var dispatcher = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains(
                "internal sealed class TelemetryDispatcher",
                StringComparison.Ordinal))).ToString();
        Assert.Contains("internal sealed class DispatcherTelemetry", dispatcher, StringComparison.Ordinal);
        Assert.Contains("dispatcher.operation.duration", dispatcher, StringComparison.Ordinal);
        Assert.Contains("activity.AddException(exception);", dispatcher, StringComparison.Ordinal);
        Assert.Contains("activity.AddEvent(new global::System.Diagnostics.ActivityEvent", dispatcher, StringComparison.Ordinal);
        Assert.Contains("HasQueryHandler(messageType, typeof(TResponse))", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("CS0436", dispatcher, StringComparison.Ordinal);

        var registration = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains(
                "public static class GeneratedDispatcherServiceCollectionExtensions",
                StringComparison.Ordinal))).ToString();
        Assert.Contains("telemetry.EnableMetrics || telemetry.EnableTracing", registration, StringComparison.Ordinal);
        Assert.Contains("typeof(global::Dispatcher.TelemetryDispatcher)", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("CS0436", registration, StringComparison.Ordinal);
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generates_typed_registrations_for_every_handler_shape()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]
            [assembly: GenerateDispatcher("AddCompleteDispatcher")]

            internal sealed record TestQuery : IQuery<string>;
            internal sealed class TestQueryHandler : IQueryHandler<TestQuery, string>
            {
                public ValueTask<string> HandleAsync(TestQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("value");
            }

            internal sealed record ResultCommand : ICommand<int>;
            internal sealed class ResultCommandHandler : ICommandHandler<ResultCommand, int>
            {
                public ValueTask<int> HandleAsync(ResultCommand command, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(1);
            }

            internal sealed record PlainCommand : ICommand;
            internal sealed class PlainCommandHandler : ICommandHandler<PlainCommand>
            {
                public ValueTask HandleAsync(PlainCommand command, CancellationToken cancellationToken) =>
                    ValueTask.CompletedTask;
            }

            internal sealed record TestNotification : INotification;
            internal sealed class TestNotificationHandler : INotificationHandler<TestNotification>
            {
                public ValueTask HandleAsync(TestNotification notification, CancellationToken cancellationToken) =>
                    ValueTask.CompletedTask;
            }
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains(
                "public static class GeneratedHandlerServiceCollectionExtensions_GeneratorTests",
                StringComparison.Ordinal))).ToString();
        Assert.Contains("namespace Dispatcher.SourceGeneration;", generated, StringComparison.Ordinal);
        Assert.Contains("AddGeneratedTestHandlers", generated, StringComparison.Ordinal);
        Assert.Contains("AddQueryHandler<", generated, StringComparison.Ordinal);
        Assert.Contains("AddCommandHandler<", generated, StringComparison.Ordinal);
        Assert.Contains("AddNotificationHandler<", generated, StringComparison.Ordinal);
        Assert.Contains("Dispatcher.ServiceCollectionExtensions", generated, StringComparison.Ordinal);
        Assert.Contains(
            "Action<global::Dispatcher.DispatcherOptions> configure",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "options.ServiceLifetime",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "handlerOptions.ServiceLifetime = options.ServiceLifetime",
            generated,
            StringComparison.Ordinal);
        Assert.DoesNotContain("MakeGenericType", generated, StringComparison.Ordinal);
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generates_key_free_open_notification_registration_and_dispatch_plan()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]
            [assembly: GenerateDispatcher("AddTestDispatcher")]

            internal sealed record TestNotification : INotification;
            internal sealed class AuditHandler<TNotification> : INotificationHandler<TNotification>
                where TNotification : INotification
            {
                public ValueTask HandleAsync(
                    TNotification notification,
                    CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var registration = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains(
                "public static class GeneratedHandlerServiceCollectionExtensions_GeneratorTests",
                StringComparison.Ordinal))).ToString();
        Assert.Contains("AddNotificationHandler(services, typeof(global::AuditHandler<>)", registration, StringComparison.Ordinal);
        Assert.Contains("IsOpenNotificationHandler_", registration, StringComparison.Ordinal);
        Assert.Contains("InvokeOpenNotificationHandler_", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("Keyed", registration, StringComparison.Ordinal);

        var dispatcher = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains("internal sealed class Dispatcher", StringComparison.Ordinal))).ToString();
        Assert.Contains("internal sealed class OpenNotificationHandlerRegistry", dispatcher, StringComparison.Ordinal);
        Assert.Contains("OpenNotificationCore<global::TestNotification>", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("MakeGenericType", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("Keyed", dispatcher, StringComparison.Ordinal);
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generates_closed_registrations_for_open_pipeline_behavior()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddTestDispatcher")]

            internal sealed record TestQuery : IQuery<string>;
            internal sealed class TestQueryHandler : IQueryHandler<TestQuery, string>
            {
                public ValueTask<string> HandleAsync(TestQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("value");
            }

            internal sealed class LoggingBehavior<TRequest, TResponse>
                : IPipelineBehavior<TRequest, TResponse>
                where TRequest : IRequest
            {
                public ValueTask<TResponse> HandleAsync(
                    TRequest request,
                    RequestHandlerDelegate<TResponse> next,
                    CancellationToken cancellationToken) => next(cancellationToken);
            }
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains(
                "public static class GeneratedPipelineBehaviorServiceCollectionExtensions",
                StringComparison.Ordinal))).ToString();
        Assert.Contains("namespace Dispatcher.SourceGeneration;", generated, StringComparison.Ordinal);
        Assert.Contains("typeof(global::LoggingBehavior<,>)", generated, StringComparison.Ordinal);
        Assert.Contains(
            "AddPipelineBehavior<global::TestQuery, string, global::LoggingBehavior<global::TestQuery, string>>",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "Action<global::Dispatcher.DispatcherOptions> configure",
            generated,
            StringComparison.Ordinal);
        Assert.Contains(
            "behaviorOptions.ServiceLifetime = options.ServiceLifetime",
            generated,
            StringComparison.Ordinal);
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Applies_constrained_behavior_only_to_compatible_commands_and_uses_unit()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddTestDispatcher")]

            internal sealed record TestQuery : IQuery<string>;
            internal sealed class TestQueryHandler : IQueryHandler<TestQuery, string>
            {
                public ValueTask<string> HandleAsync(TestQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("value");
            }
            internal sealed record TestCommand : ICommand;
            internal sealed class TestCommandHandler : ICommandHandler<TestCommand>
            {
                public ValueTask HandleAsync(TestCommand command, CancellationToken cancellationToken) =>
                    ValueTask.CompletedTask;
            }
            internal sealed class CommandBehavior<TCommand, TResponse>
                : IPipelineBehavior<TCommand, TResponse>
                where TCommand : ICommand<TResponse>
            {
                public ValueTask<TResponse> HandleAsync(
                    TCommand request,
                    RequestHandlerDelegate<TResponse> next,
                    CancellationToken cancellationToken) => next(cancellationToken);
            }
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains(
                "public static class GeneratedPipelineBehaviorServiceCollectionExtensions",
                StringComparison.Ordinal))).ToString();
        Assert.Contains(
            "AddPipelineBehavior<global::TestCommand, global::Dispatcher.Unit, global::CommandBehavior<global::TestCommand, global::Dispatcher.Unit>>",
            generated,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "CommandBehavior<global::TestQuery, string>",
            generated,
            StringComparison.Ordinal);
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Applies_unmanaged_behavior_only_to_unmanaged_requests()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddTestDispatcher")]

            internal readonly record struct UnmanagedQuery(int Value) : IQuery<int>;
            internal sealed class UnmanagedQueryHandler : IQueryHandler<UnmanagedQuery, int>
            {
                public ValueTask<int> HandleAsync(UnmanagedQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(query.Value);
            }
            internal readonly record struct ManagedQuery(string Value) : IQuery<int>;
            internal sealed class ManagedQueryHandler : IQueryHandler<ManagedQuery, int>
            {
                public ValueTask<int> HandleAsync(ManagedQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(query.Value.Length);
            }
            internal sealed class UnmanagedBehavior<TRequest, TResponse>
                : IPipelineBehavior<TRequest, TResponse>
                where TRequest : unmanaged, IRequest
            {
                public ValueTask<TResponse> HandleAsync(
                    TRequest request,
                    RequestHandlerDelegate<TResponse> next,
                    CancellationToken cancellationToken) => next(cancellationToken);
            }
            """;

        var result = GeneratorTestHarness.Run(source);

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains(
                "public static class GeneratedPipelineBehaviorServiceCollectionExtensions",
                StringComparison.Ordinal))).ToString();
        Assert.Contains(
            "UnmanagedBehavior<global::UnmanagedQuery, int>",
            generated,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "UnmanagedBehavior<global::ManagedQuery, int>",
            generated,
            StringComparison.Ordinal);
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generates_source_generation_types_in_expected_namespaces()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddTestHandlers")]
            [assembly: GenerateDispatcher("AddTestDispatcher")]
            """;

        var result = GeneratorTestHarness.Run(source, assemblyName: "Company.class");

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Contains(result.GeneratedTrees, tree =>
            tree.ToString().Contains(
                "namespace Dispatcher.SourceGeneration;\n\n[global::System.AttributeUsage",
                StringComparison.Ordinal) &&
            tree.ToString().Contains("GenerateDispatcherHandlersAttribute", StringComparison.Ordinal));
        Assert.Contains(result.GeneratedTrees, tree =>
            tree.ToString().Contains(
                "namespace Dispatcher.SourceGeneration;\n\n[global::System.AttributeUsage",
                StringComparison.Ordinal) &&
            tree.ToString().Contains("GenerateDispatcherAttribute", StringComparison.Ordinal));
        Assert.Contains(result.GeneratedTrees, tree =>
            tree.ToString().Contains(
                "namespace Dispatcher.SourceGeneration;",
                StringComparison.Ordinal) &&
            tree.ToString().Contains(
                "public static class GeneratedDispatcherServiceCollectionExtensions",
                StringComparison.Ordinal));
        Assert.Contains(result.GeneratedTrees, tree =>
            tree.ToString().Contains(
                "namespace Dispatcher.SourceGeneration;",
                StringComparison.Ordinal) &&
            tree.ToString().Contains(
                "public static class GeneratedHandlerServiceCollectionExtensions_Company_class",
                StringComparison.Ordinal));
        Assert.Contains(result.GeneratedTrees, tree =>
            tree.ToString().Contains(
                "namespace Dispatcher;\n\ninternal sealed class Dispatcher",
                StringComparison.Ordinal));
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }
}