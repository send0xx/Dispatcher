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
}

internal sealed class OpenGenericNotificationHandlerModel(INamedTypeSymbol implementationType)
{
    public INamedTypeSymbol ImplementationType { get; } = implementationType;
    public ITypeParameterSymbol TypeParameter { get; } = implementationType.TypeParameters[0];
    public bool CanInvokeFromOtherAssemblies => TypeParameter.ConstraintTypes.All(IsPubliclyAccessible);
    public string SortKey => ImplementationType.ToDisplayString(SymbolDisplayFormats.FullyQualified);
    public string RegistrationClassName => "global::Dispatcher.SourceGeneration." +
        "GeneratedHandlerServiceCollectionExtensions_" +
        IdentifierSanitizer.SanitizeIdentifier(ImplementationType.ContainingAssembly.Name);
    public string MethodSuffix => IdentifierSanitizer.SanitizeIdentifier(
        ImplementationType.ToDisplayString(SymbolDisplayFormats.FullyQualified)) + "_" +
        GetStableHash(ImplementationType.ToDisplayString(SymbolDisplayFormats.FullyQualified)).ToString("X8");

    private static uint GetStableHash(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }

        return hash;
    }

    private static bool IsPubliclyAccessible(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol arrayType => IsPubliclyAccessible(arrayType.ElementType),
        IPointerTypeSymbol pointerType => IsPubliclyAccessible(pointerType.PointedAtType),
        INamedTypeSymbol namedType =>
            namedType.DeclaredAccessibility == Accessibility.Public &&
            (namedType.ContainingType is null || IsPubliclyAccessible(namedType.ContainingType)) &&
            namedType.TypeArguments.All(IsPubliclyAccessible),
        _ => true
    };
}

internal sealed class DispatchRouteModel(
    INamedTypeSymbol messageType,
    HandlerModel? handler,
    ImmutableArray<OpenGenericNotificationHandlerModel> openNotificationHandlers)
{
    public INamedTypeSymbol MessageType { get; } = messageType;
    public HandlerModel? Handler { get; } = handler;
    public ImmutableArray<OpenGenericNotificationHandlerModel> OpenNotificationHandlers { get; } =
        openNotificationHandlers;
}

internal sealed class GenerationResult
{
    public static readonly GenerationResult Empty = new(
        null,
        null,
        ImmutableArray<HandlerModel>.Empty,
        ImmutableArray<HandlerModel>.Empty,
        ImmutableArray<OpenGenericNotificationHandlerModel>.Empty,
        ImmutableArray<OpenGenericNotificationHandlerModel>.Empty,
        ImmutableArray<DispatchRouteModel>.Empty,
        ImmutableArray<INamedTypeSymbol>.Empty,
        ImmutableArray<Diagnostic>.Empty);

    public GenerationResult(
        string? methodName,
        string? dispatcherMethodName,
        ImmutableArray<HandlerModel> localHandlers,
        ImmutableArray<HandlerModel> dispatchHandlers,
        ImmutableArray<OpenGenericNotificationHandlerModel> localOpenNotificationHandlers,
        ImmutableArray<OpenGenericNotificationHandlerModel> dispatchOpenNotificationHandlers,
        ImmutableArray<DispatchRouteModel> dispatchRoutes,
        ImmutableArray<INamedTypeSymbol> openBehaviors,
        ImmutableArray<Diagnostic> diagnostics,
        string assemblyName = "",
        INamedTypeSymbol? unitType = null)
    {
        MethodName = methodName;
        DispatcherMethodName = dispatcherMethodName;
        LocalHandlers = localHandlers;
        DispatchHandlers = dispatchHandlers;
        LocalOpenNotificationHandlers = localOpenNotificationHandlers;
        DispatchOpenNotificationHandlers = dispatchOpenNotificationHandlers;
        DispatchRoutes = dispatchRoutes;
        OpenBehaviors = openBehaviors;
        Diagnostics = diagnostics;
        AssemblyName = assemblyName;
        UnitType = unitType;
    }

    public string? MethodName { get; }
    public string? DispatcherMethodName { get; }
    public ImmutableArray<HandlerModel> LocalHandlers { get; }
    public ImmutableArray<HandlerModel> DispatchHandlers { get; }
    public ImmutableArray<OpenGenericNotificationHandlerModel> LocalOpenNotificationHandlers { get; }
    public ImmutableArray<OpenGenericNotificationHandlerModel> DispatchOpenNotificationHandlers { get; }
    public ImmutableArray<DispatchRouteModel> DispatchRoutes { get; }
    public ImmutableArray<INamedTypeSymbol> OpenBehaviors { get; }
    public ImmutableArray<Diagnostic> Diagnostics { get; }
    public string AssemblyName { get; }
    public INamedTypeSymbol? UnitType { get; }
}