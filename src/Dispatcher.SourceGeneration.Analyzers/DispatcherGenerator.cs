using System.Text;
using Dispatcher.SourceGeneration.Analysis;
using Dispatcher.SourceGeneration.Emission;
using Dispatcher.SourceGeneration.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Dispatcher.SourceGeneration;

/// <summary>
/// Generates Dispatcher registrations and dispatch infrastructure for opted-in assemblies.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class DispatcherGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static output =>
        {
            output.AddSource(
                "GenerateDispatcherHandlersAttribute.g.cs",
                SourceText.From(GeneratedAttributes.HandlerRegistration, Encoding.UTF8));
            output.AddSource(
                "GenerateDispatcherAttribute.g.cs",
                SourceText.From(GeneratedAttributes.DispatcherRegistration, Encoding.UTF8));
        });

        var model = context.CompilationProvider.Select(static (compilation, cancellationToken) =>
            CompilationAnalyzer.Analyze(compilation, cancellationToken));
        context.RegisterSourceOutput(model, static (output, generation) =>
            SourceOutputEmitter.Emit(output, generation));
    }
}