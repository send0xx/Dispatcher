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
        "DSPG003", "Unsupported open generic handler",
        "Handler '{0}' is generic but is not a supported open generic handler. Use a closed handler " +
        "type that is not nested in a generic type, or an open generic notification handler with one " +
        "type parameter that implements INotificationHandler<TNotification> using that parameter directly");
    internal static readonly DiagnosticDescriptor HandlerCannotBeActivated = Create(
        "DSPG004", "Handler cannot be activated",
        "Handler '{0}' must be accessible and expose at least one public constructor");
    internal static readonly DiagnosticDescriptor MissingRequestHandler = Create(
        "DSPG005", "Request has no handler",
        "Request '{0}' has no handler in this assembly or in the modules it references");
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
    internal static readonly DiagnosticDescriptor UnregisteredLocalHandlers = Create(
        "DSPG010", "Generated dispatcher routes to unregistered handlers",
        "Assembly '{0}' declares handlers that its generated dispatcher routes to but does not apply " +
        "GenerateDispatcherHandlersAttribute, so nothing registers them and dispatch fails at runtime");

    private static DiagnosticDescriptor Create(string id, string title, string message) => new(
        id,
        title,
        message,
        "Dispatcher.SourceGeneration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}