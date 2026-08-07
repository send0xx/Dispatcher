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
        Assert.Contains(result.GeneratedTrees, tree =>
            tree.ToString().Contains("AddTestDispatcher", StringComparison.Ordinal));
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generates_typed_registrations_for_every_handler_shape()
    {
        const string source = """
            using Dispatcher;
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
            tree.ToString().Contains("DispatcherGeneratedExtensions", StringComparison.Ordinal))).ToString();
        Assert.Contains("AddGeneratedTestHandlers", generated, StringComparison.Ordinal);
        Assert.Contains("AddQueryHandler<", generated, StringComparison.Ordinal);
        Assert.Contains("AddCommandHandler<", generated, StringComparison.Ordinal);
        Assert.Contains("AddNotificationHandler<", generated, StringComparison.Ordinal);
        Assert.Contains("Dispatcher.Extensions.Microsoft.DependencyInjection", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("MakeGenericType", generated, StringComparison.Ordinal);
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Generates_closed_registrations_for_open_pipeline_behavior()
    {
        const string source = """
            using Dispatcher;
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
                "GeneratedPipelineBehaviorServiceCollectionExtensions",
                StringComparison.Ordinal))).ToString();
        Assert.Contains("typeof(global::LoggingBehavior<,>)", generated, StringComparison.Ordinal);
        Assert.Contains(
            "AddPipelineBehavior<global::TestQuery, string, global::LoggingBehavior<global::TestQuery, string>>",
            generated,
            StringComparison.Ordinal);
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }
}