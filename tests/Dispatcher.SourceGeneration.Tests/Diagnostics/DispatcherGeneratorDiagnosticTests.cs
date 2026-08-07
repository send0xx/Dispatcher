using Dispatcher.SourceGeneration.Tests.TestSupport;
using Xunit;

namespace Dispatcher.SourceGeneration.Tests.Diagnostics;

public sealed class DispatcherGeneratorDiagnosticTests
{
    [Fact]
    public void Reports_duplicate_request_handlers()
    {
        const string source = """
            using Dispatcher;
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
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]
            internal sealed record GenericQuery<T> : IQuery<T>;
            internal sealed class GenericHandler<T> : IQueryHandler<GenericQuery<T>, T>
            {
                public ValueTask<T> HandleAsync(GenericQuery<T> query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(default(T)!);
            }
            """;

        AssertDiagnostic(source, "DSPG003");
    }

    [Fact]
    public void Reports_invalid_generated_method_name()
    {
        const string source = """
            using Dispatcher;
            [assembly: GenerateDispatcherHandlers("not valid")]
            """;

        AssertDiagnostic(source, "DSPG001");
    }

    [Fact]
    public void Reports_invalid_generated_dispatcher_class_name()
    {
        const string source = """
            using Dispatcher;
            [assembly: GenerateDispatcher("not valid")]
            """;

        AssertDiagnostic(source, "DSPG006");
    }

    [Fact]
    public void Reports_handler_without_public_constructor()
    {
        const string source = """
            using Dispatcher;
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
    public void Reports_unsupported_open_generic_behavior_shape()
    {
        const string source = """
            using Dispatcher;
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

    private static void AssertDiagnostic(string source, string diagnosticId) =>
        Assert.Contains(
            GeneratorTestHarness.Run(source).Diagnostics,
            diagnostic => diagnostic.Id == diagnosticId);
}
