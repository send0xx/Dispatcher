using System.Collections.Immutable;
using Dispatcher.Extensions.DependencyInjection;
using Dispatcher.SourceGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.SourceGeneration.Tests;

public sealed class DispatcherGeneratorTests
{
    [Fact]
    public void Generates_typed_registrations_for_every_handler_shape()
    {
        const string source = """
            using Dispatcher;
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]

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

        var result = RunGenerator(source);

        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains("DispatcherGeneratedExtensions", StringComparison.Ordinal))).ToString();
        Assert.Contains("AddGeneratedTestHandlers", generated, StringComparison.Ordinal);
        Assert.Contains("AddQueryHandler<", generated, StringComparison.Ordinal);
        Assert.Contains("AddCommandHandler<", generated, StringComparison.Ordinal);
        Assert.Contains("AddNotificationHandler<", generated, StringComparison.Ordinal);
        Assert.DoesNotContain("MakeGenericType", generated, StringComparison.Ordinal);
        Assert.Empty(result.OutputCompilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

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

        Assert.Contains(RunGenerator(source).Diagnostics, diagnostic => diagnostic.Id == "DSPG002");
    }

    [Fact]
    public void Reports_request_without_handler()
    {
        const string source = """
            using Dispatcher;
            [assembly: GenerateDispatcherHandlers("AddGeneratedTestHandlers")]
            internal sealed record MissingQuery : IQuery<int>;
            """;

        Assert.Contains(RunGenerator(source).Diagnostics, diagnostic => diagnostic.Id == "DSPG005");
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

        Assert.Contains(RunGenerator(source).Diagnostics, diagnostic => diagnostic.Id == "DSPG003");
    }

    [Fact]
    public void Reports_invalid_generated_method_name()
    {
        const string source = """
            using Dispatcher;
            [assembly: GenerateDispatcherHandlers("not valid")]
            """;

        Assert.Contains(RunGenerator(source).Diagnostics, diagnostic => diagnostic.Id == "DSPG001");
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

        Assert.Contains(RunGenerator(source).Diagnostics, diagnostic => diagnostic.Id == "DSPG004");
    }

    private static GeneratorTestResult RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            "global using System.Threading; global using System.Threading.Tasks;" + source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Concat(
            [
                MetadataReference.CreateFromFile(typeof(IRequest).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TypedDispatcherServiceCollectionExtensions).Assembly.Location)
            ])
            .Distinct(MetadataReferencePathComparer.Instance);
        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new DispatcherGenerator())
            .WithUpdatedParseOptions((CSharpParseOptions)syntaxTree.Options);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);
        var runResult = driver.GetRunResult();

        return new GeneratorTestResult(
            diagnostics.AddRange(runResult.Diagnostics),
            runResult.GeneratedTrees,
            outputCompilation);
    }

    private sealed class MetadataReferencePathComparer : IEqualityComparer<MetadataReference>
    {
        public static readonly MetadataReferencePathComparer Instance = new();

        public bool Equals(MetadataReference? x, MetadataReference? y) =>
            string.Equals(x?.Display, y?.Display, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(MetadataReference obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Display ?? string.Empty);
    }

    private sealed record GeneratorTestResult(
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableArray<SyntaxTree> GeneratedTrees,
        Compilation OutputCompilation);
}