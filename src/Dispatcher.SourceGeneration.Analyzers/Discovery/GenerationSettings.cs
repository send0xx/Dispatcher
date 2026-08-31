using System.Collections.Immutable;
using Dispatcher.SourceGeneration.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dispatcher.SourceGeneration.Discovery;

internal sealed class GenerationSettings
{
    private const string HandlerAttributeName =
        "Dispatcher.SourceGeneration.GenerateDispatcherHandlersAttribute";
    private const string DispatcherAttributeName =
        "Dispatcher.SourceGeneration.GenerateDispatcherAttribute";

    private GenerationSettings(
        AttributeData? handlerAttribute,
        AttributeData? dispatcherAttribute,
        string? handlerMethod,
        string? dispatcherMethod,
        ImmutableArray<Diagnostic> diagnostics)
    {
        HandlerAttribute = handlerAttribute;
        DispatcherAttribute = dispatcherAttribute;
        HandlerMethod = handlerMethod;
        DispatcherMethod = dispatcherMethod;
        Diagnostics = diagnostics;
    }

    internal AttributeData? HandlerAttribute { get; }
    internal AttributeData? DispatcherAttribute { get; }
    internal string? HandlerMethod { get; }
    internal string? DispatcherMethod { get; }
    internal ImmutableArray<Diagnostic> Diagnostics { get; }
    internal bool HasErrors => Diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    internal static GenerationSettings? Read(Compilation compilation, CancellationToken cancellationToken)
    {
        var handlerAttributeType = compilation.GetTypeByMetadataName(HandlerAttributeName);
        var dispatcherAttributeType = compilation.GetTypeByMetadataName(DispatcherAttributeName);
        if (handlerAttributeType is null || dispatcherAttributeType is null)
        {
            return null;
        }

        var handlerAttribute = Find(compilation.Assembly, handlerAttributeType);
        var dispatcherAttribute = Find(compilation.Assembly, dispatcherAttributeType);
        if (handlerAttribute is null && dispatcherAttribute is null)
        {
            return null;
        }

        var handlerMethod = MethodName(handlerAttribute);
        var dispatcherMethod = MethodName(dispatcherAttribute);
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        if (handlerAttribute is not null && !IsValidMethodName(handlerMethod))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticCatalog.InvalidHandlerRegistrationName,
                Location(handlerAttribute, cancellationToken),
                handlerMethod ?? ""));
            handlerMethod = null;
        }
        else if (dispatcherAttribute is not null && !IsValidMethodName(dispatcherMethod))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticCatalog.InvalidDispatcherRegistrationName,
                Location(dispatcherAttribute, cancellationToken),
                dispatcherMethod ?? ""));
            dispatcherMethod = null;
        }

        return new GenerationSettings(
            handlerAttribute,
            dispatcherAttribute,
            handlerMethod,
            dispatcherMethod,
            diagnostics.ToImmutable());
    }

    private static AttributeData? Find(IAssemblySymbol assembly, INamedTypeSymbol type) =>
        assembly.GetAttributes().FirstOrDefault(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, type));

    private static string? MethodName(AttributeData? attribute) =>
        attribute?.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as string
            : null;

    private static Location? Location(AttributeData attribute, CancellationToken cancellationToken) =>
        attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation();

    private static bool IsValidMethodName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        SyntaxFacts.IsValidIdentifier(value) &&
        SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None;
}