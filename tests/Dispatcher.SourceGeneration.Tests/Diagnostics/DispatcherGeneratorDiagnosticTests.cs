using Dispatcher.SourceGeneration.Tests.TestSupport;
using Xunit;

namespace Dispatcher.SourceGeneration.Tests.Diagnostics;

public sealed class DispatcherGeneratorDiagnosticTests
{
    [Fact]
    public void Reports_ambiguous_polymorphic_handler_routes()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]
            [assembly: GenerateDispatcher("AddTestDispatcher")]

            internal interface IFirstQuery : IQuery<string>;
            internal interface ISecondQuery : IQuery<string>;
            internal sealed record AmbiguousQuery : IFirstQuery, ISecondQuery;

            internal sealed class FirstHandler : IQueryHandler<IFirstQuery, string>
            {
                public ValueTask<string> HandleAsync(IFirstQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("first");
            }

            internal sealed class SecondHandler : IQueryHandler<ISecondQuery, string>
            {
                public ValueTask<string> HandleAsync(ISecondQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("second");
            }
            """;

        AssertDiagnostic(source, "DSPG009");
    }

    [Fact]
    public void Reports_duplicate_request_handlers()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]
            internal sealed record TestQuery : IQuery<string>;
            internal sealed class FirstHandler : IQueryHandler<TestQuery, string>
            {
                public ValueTask<string> HandleAsync(TestQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("first");
            }
            internal sealed class SecondHandler : IQueryHandler<TestQuery, string>
            {
                public ValueTask<string> HandleAsync(TestQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("second");
            }
            """;

        AssertDiagnostic(source, "DSPG002");
    }

    [Fact]
    public void Reports_request_without_handler()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]
            internal sealed record MissingQuery : IQuery<int>;
            """;

        AssertDiagnostic(source, "DSPG005");
    }

    [Fact]
    public void Reports_open_generic_handler()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]
            internal sealed record GenericQuery<T> : IQuery<T>;
            internal sealed class GenericHandler<T> : IQueryHandler<GenericQuery<T>, T>
            {
                public ValueTask<T> HandleAsync(GenericQuery<T> query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(default(T)!);
            }
            """;

        var diagnostic = Assert.Single(
            GeneratorTestHarness.Run(source).Diagnostics
                .Where(candidate => candidate.Id == "DSPG003")
                .Select(candidate => candidate.GetMessage())
                .Distinct(StringComparer.Ordinal));

        Assert.Contains("Use a closed handler type", diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_handler_nested_in_a_generic_type()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]
            internal sealed record Ping : INotification;
            internal static class Outer<T>
            {
                internal sealed class PingHandler : INotificationHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping notification, CancellationToken cancellationToken) =>
                        ValueTask.CompletedTask;
                }
            }
            """;

        AssertDiagnostic(source, "DSPG003");
    }

    [Fact]
    public void Reports_open_notification_handler_nested_in_a_generic_type()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]
            internal static class Outer<T>
            {
                internal sealed class AuditHandler<TNotification> : INotificationHandler<TNotification>
                    where TNotification : INotification
                {
                    public ValueTask HandleAsync(
                        TNotification notification,
                        CancellationToken cancellationToken) => ValueTask.CompletedTask;
                }
            }
            """;

        AssertDiagnostic(source, "DSPG003");
    }

    [Fact]
    public void Reports_invalid_generated_method_name()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("not valid")]
            """;

        AssertDiagnostic(source, "DSPG001");
    }

    [Fact]
    public void Reports_invalid_generated_dispatcher_class_name()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("not valid")]
            """;

        AssertDiagnostic(source, "DSPG006");
    }

    [Fact]
    public void Reports_handler_without_public_constructor()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]
            internal sealed record TestQuery : IQuery<string>;
            internal sealed class TestHandler : IQueryHandler<TestQuery, string>
            {
                private TestHandler() { }
                public ValueTask<string> HandleAsync(TestQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("value");
            }
            """;

        AssertDiagnostic(source, "DSPG004");
    }

    [Fact]
    public void Reports_inaccessible_open_notification_handler()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]
            internal static class HandlerContainer
            {
                private sealed class AuditHandler<TNotification> : INotificationHandler<TNotification>
                    where TNotification : INotification
                {
                    public AuditHandler() { }
                    public ValueTask HandleAsync(
                        TNotification notification,
                        CancellationToken cancellationToken) => ValueTask.CompletedTask;
                }
            }
            """;

        AssertDiagnostic(source, "DSPG004");
    }

    [Fact]
    public void Reports_unsupported_open_generic_behavior_shape()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddDispatcher")]
            internal sealed class InvalidBehavior<TRequest>
                : IPipelineBehavior<TRequest, string>
                where TRequest : IRequest
            {
                public ValueTask<string> HandleAsync(
                    TRequest request,
                    RequestHandlerDelegate<string> next,
                    CancellationToken cancellationToken) => next(cancellationToken);
            }
            """;

        AssertDiagnostic(source, "DSPG008");
    }

    [Fact]
    public void Reports_local_handlers_that_the_generated_dispatcher_never_registers()
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

        AssertDiagnostic(source, "DSPG010");
    }

    [Fact]
    public void Reports_local_open_notification_handlers_that_are_never_registered()
    {
        const string source = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
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

        AssertDiagnostic(source, "DSPG010");
    }

    [Fact]
    public void Does_not_report_a_host_dispatcher_that_declares_no_handlers()
    {
        var module = GeneratorTestHarness.CompileModule(
            """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddModuleHandlers")]
            namespace Module;
            public sealed record ModuleQuery : IQuery<string>;
            internal sealed class ModuleQueryHandler : IQueryHandler<ModuleQuery, string>
            {
                public ValueTask<string> HandleAsync(ModuleQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("module");
            }
            """,
            "Registered.Module");

        var result = GeneratorTestHarness.Run(
            """
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddDispatcher")]
            """,
            additionalReferences: [module],
            assemblyName: "Registered.Host");

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "DSPG010");
    }

    private static void AssertDiagnostic(string source, string diagnosticId) =>
        Assert.Contains(
            GeneratorTestHarness.Run(source).Diagnostics,
            diagnostic => diagnostic.Id == diagnosticId);
}