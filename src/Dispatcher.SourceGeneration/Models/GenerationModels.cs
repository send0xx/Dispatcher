using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Models;

internal enum HandlerModelKind
{
    Query,
    CommandWithResponse,
    Command,
    Notification
}

internal sealed class HandlerModel
{
    public HandlerModel(
        HandlerModelKind kind,
        ITypeSymbol messageType,
        ITypeSymbol? responseType,
        INamedTypeSymbol implementationType)
    {
        Kind = kind;
        MessageType = messageType;
        ResponseType = responseType;
        ImplementationType = implementationType;
    }

    public HandlerModelKind Kind { get; }
    public ITypeSymbol MessageType { get; }
    public ITypeSymbol? ResponseType { get; }
    public INamedTypeSymbol ImplementationType { get; }
    public string SortKey => MessageType.ToDisplayString(SymbolDisplayFormats.FullyQualified) + "|" +
        ImplementationType.ToDisplayString(SymbolDisplayFormats.FullyQualified);
    public string MethodName => Kind switch
    {
        HandlerModelKind.Query => "AddQueryHandler",
        HandlerModelKind.CommandWithResponse => "AddCommandHandler",
        HandlerModelKind.Command => "AddCommandHandler",
        _ => "AddNotificationHandler"
    };
    public string TypeArguments => ResponseType is null
        ? MessageType.ToDisplayString(SymbolDisplayFormats.FullyQualified) + ", " +
          ImplementationType.ToDisplayString(SymbolDisplayFormats.FullyQualified)
        : MessageType.ToDisplayString(SymbolDisplayFormats.FullyQualified) + ", " +
          ResponseType.ToDisplayString(SymbolDisplayFormats.FullyQualified) + ", " +
          ImplementationType.ToDisplayString(SymbolDisplayFormats.FullyQualified);
    public string ServiceType => Kind switch
    {
        HandlerModelKind.Query => "global::Dispatcher.IQueryHandler<" +
            MessageType.ToDisplayString(SymbolDisplayFormats.FullyQualified) + ", " +
            ResponseType!.ToDisplayString(SymbolDisplayFormats.FullyQualified) + ">",
        HandlerModelKind.CommandWithResponse => "global::Dispatcher.ICommandHandler<" +
            MessageType.ToDisplayString(SymbolDisplayFormats.FullyQualified) + ", " +
            ResponseType!.ToDisplayString(SymbolDisplayFormats.FullyQualified) + ">",
        HandlerModelKind.Command => "global::Dispatcher.ICommandHandler<" +
            MessageType.ToDisplayString(SymbolDisplayFormats.FullyQualified) + ">",
        _ => "global::Dispatcher.INotificationHandler<" +
            MessageType.ToDisplayString(SymbolDisplayFormats.FullyQualified) + ">"
    };
}

internal sealed class GenerationResult
{
    public static readonly GenerationResult Empty = new(
        null,
        null,
        ImmutableArray<HandlerModel>.Empty,
        ImmutableArray<HandlerModel>.Empty,
        ImmutableArray<INamedTypeSymbol>.Empty,
        ImmutableArray<Diagnostic>.Empty);

    public GenerationResult(
        string? methodName,
        string? dispatcherMethodName,
        ImmutableArray<HandlerModel> localHandlers,
        ImmutableArray<HandlerModel> dispatchHandlers,
        ImmutableArray<INamedTypeSymbol> openBehaviors,
        ImmutableArray<Diagnostic> diagnostics,
        string assemblyName = "")
    {
        MethodName = methodName;
        DispatcherMethodName = dispatcherMethodName;
        LocalHandlers = localHandlers;
        DispatchHandlers = dispatchHandlers;
        OpenBehaviors = openBehaviors;
        Diagnostics = diagnostics;
        AssemblyName = assemblyName;
    }

    public string? MethodName { get; }
    public string? DispatcherMethodName { get; }
    public ImmutableArray<HandlerModel> LocalHandlers { get; }
    public ImmutableArray<HandlerModel> DispatchHandlers { get; }
    public ImmutableArray<INamedTypeSymbol> OpenBehaviors { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
    public string AssemblyName { get; }
}