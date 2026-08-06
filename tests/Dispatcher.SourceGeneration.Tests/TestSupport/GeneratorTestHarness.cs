using System.Collections.Immutable;
using Dispatcher.Extensions.Microsoft.DependencyInjection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.SourceGeneration.Tests.TestSupport;

internal static class GeneratorTestHarness
{
    internal static GeneratorTestResult Run(string source)
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
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
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
        internal static readonly MetadataReferencePathComparer Instance = new();

        public bool Equals(MetadataReference? x, MetadataReference? y) =>
            string.Equals(x?.Display, y?.Display, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(MetadataReference obj) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Display ?? string.Empty);
    }
}

internal sealed record GeneratorTestResult(
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<SyntaxTree> GeneratedTrees,
    Compilation OutputCompilation);