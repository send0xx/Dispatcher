using System.Text;
using Dispatcher.SourceGeneration.Analysis;
using Dispatcher.SourceGeneration.Emission;
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
        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddSource(
                "GenerateDispatcherHandlersAttribute.g.cs",
                SourceText.From(InjectedSources.HandlerAttribute, Encoding.UTF8));
            postInitializationContext.AddSource(
                "GenerateDispatcherAttribute.g.cs",
                SourceText.From(InjectedSources.DispatcherAttribute, Encoding.UTF8));
        });

        var model = context.CompilationProvider.Select(static (compilation, cancellationToken) =>
            DispatcherAnalyzer.Analyze(compilation, cancellationToken));

        context.RegisterSourceOutput(model, static (productionContext, result) =>
            DispatcherEmitter.Emit(productionContext, result));
    }
}