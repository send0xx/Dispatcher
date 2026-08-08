using System.Collections.Immutable;
using Dispatcher.DependencyInjection.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.SourceGeneration.Tests.TestSupport;

internal static class GeneratorTestHarness
{
    internal static GeneratorTestResult Run(
        string source,
        bool includeRuntimeIntegration = true,
        IEnumerable<MetadataReference>? additionalReferences = null,
        string assemblyName = "GeneratorTests")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            "global using System.Threading; global using System.Threading.Tasks;" + source,
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Concat(GetDispatcherReferences(includeRuntimeIntegration))
            .Concat(additionalReferences ?? [])
            .Distinct(MetadataReferencePathComparer.Instance);
        var compilation = CSharpCompilation.Create(
            assemblyName,
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

    internal static MetadataReference CompileModule(
        string source,
        string assemblyName,
        IEnumerable<MetadataReference>? additionalReferences = null)
    {
        var result = Run(
            source,
            additionalReferences: additionalReferences,
            assemblyName: assemblyName);
        var errors = result.Diagnostics
            .Concat(result.OutputCompilation.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(error => error.ToString())));
        }

        using var stream = new MemoryStream();
        var emitResult = result.OutputCompilation.Emit(stream);
        if (!emitResult.Success)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                emitResult.Diagnostics.Select(diagnostic => diagnostic.ToString())));
        }

        return AssemblyMetadata.CreateFromImage(stream.ToArray())
            .GetReference(display: assemblyName + ".dll");
    }

    private static IEnumerable<MetadataReference> GetDispatcherReferences(bool includeRuntimeIntegration)
    {
        yield return MetadataReference.CreateFromFile(typeof(IRequest).Assembly.Location);
        yield return MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location);

        if (includeRuntimeIntegration)
        {
            yield return MetadataReference.CreateFromFile(
                typeof(TypedDispatcherServiceCollectionExtensions).Assembly.Location);
        }
    }

    private sealed class MetadataReferencePathComparer : IEqualityComparer<MetadataReference>
    {
        internal static readonly MetadataReferencePathComparer Instance = new();

        public bool Equals(MetadataReference? x, MetadataReference? y)
        {
            if (x?.Display is null || y?.Display is null)
            {
                return ReferenceEquals(x, y);
            }

            return string.Equals(x.Display, y.Display, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(MetadataReference obj) =>
            obj.Display is null
                ? obj.GetHashCode()
                : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Display);
    }
}

internal sealed record GeneratorTestResult(
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<SyntaxTree> GeneratedTrees,
    Compilation OutputCompilation);
