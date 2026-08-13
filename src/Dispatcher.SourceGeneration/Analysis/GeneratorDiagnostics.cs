using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Analysis;

internal static class GeneratorDiagnostics
{
    internal static readonly DiagnosticDescriptor InvalidMethodName = Create(
        "DSPG001", "Invalid generated registration method name",
        "'{0}' is not a valid generated registration method name");
    internal static readonly DiagnosticDescriptor DuplicateRequestHandler = Create(
        "DSPG002", "Multiple request handlers", "Request '{0}' has multiple handlers: {1}");
    internal static readonly DiagnosticDescriptor OpenGenericHandler = Create(
        "DSPG003", "Open generic handlers are unsupported",
        "Handler '{0}' is open generic and cannot be registered by the generator");
    internal static readonly DiagnosticDescriptor HandlerCannotBeActivated = Create(
        "DSPG004", "Handler cannot be activated",
        "Handler '{0}' must be accessible and expose at least one public constructor");
    internal static readonly DiagnosticDescriptor MissingRequestHandler = Create(
        "DSPG005", "Request has no handler", "Request '{0}' has no handler in this generated module");
    internal static readonly DiagnosticDescriptor InvalidDispatcherMethodName = Create(
        "DSPG006", "Invalid generated dispatcher registration method name",
        "'{0}' is not a valid generated dispatcher registration method name");
    internal static readonly DiagnosticDescriptor InaccessibleReferencedMessage = Create(
        "DSPG007", "Referenced message is inaccessible",
        "Message '{0}' handled by module '{1}' must be accessible to the generated host dispatcher");
    internal static readonly DiagnosticDescriptor UnsupportedOpenGenericBehavior = Create(
        "DSPG008", "Unsupported open generic pipeline behavior",
        "Pipeline behavior '{0}' must have two type parameters, implement " +
        "IPipelineBehavior<TRequest, TResponse> using them in order, and expose a public constructor");
    internal static readonly DiagnosticDescriptor AmbiguousHandlerRoute = Create(
        "DSPG009", "Ambiguous polymorphic handler route",
        "Message '{0}' matches multiple equally specific handled message types: {1}");

    private static DiagnosticDescriptor Create(string id, string title, string message) => new(
        id,
        title,
        message,
        "Dispatcher.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}