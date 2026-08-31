using System.Collections.Immutable;
using Dispatcher.SourceGeneration.Diagnostics;
using Dispatcher.SourceGeneration.Model;
using Dispatcher.SourceGeneration.Support;
using Microsoft.CodeAnalysis;

namespace Dispatcher.SourceGeneration.Discovery;

internal static class HandlerScanner
{
    internal static HandlerInventory ScanCurrentAssembly(
        Compilation compilation,
        DispatcherSymbols symbols,
        ImmutableArray<INamedTypeSymbol> types,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        var closed = ImmutableArray.CreateBuilder<HandlerDefinition>();
        var open = ImmutableArray.CreateBuilder<OpenNotificationHandlerDefinition>();

        foreach (var type in types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var interfaces = HandlerInterfaces(type, symbols);
            if (interfaces.IsDefaultOrEmpty)
            {
                continue;
            }

            if (IsOpen(type, interfaces))
            {
                ScanOpenHandler(compilation, symbols, type, interfaces, open, diagnostics);
                continue;
            }

            if (!CanActivate(compilation, type))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticCatalog.HandlerCannotBeActivated,
                    type.Locations.FirstOrDefault(),
                    CSharpNames.Type(type)));
                continue;
            }

            foreach (var handlerInterface in interfaces)
            {
                closed.Add(Create(type, handlerInterface, symbols));
            }
        }

        return Inventory(closed, open);
    }

    internal static HandlerInventory ScanReferencedModules(
        Compilation compilation,
        DispatcherSymbols symbols,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        var closed = ImmutableArray.CreateBuilder<HandlerDefinition>();
        var open = ImmutableArray.CreateBuilder<OpenNotificationHandlerDefinition>();

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols
                     .Where(AssemblyTypes.HasGeneratedHandlerRegistration))
        {
            foreach (var type in AssemblyTypes.Enumerate(assembly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var interfaces = HandlerInterfaces(type, symbols);
                if (IsSupportedOpenNotificationHandler(type, interfaces, symbols.NotificationHandler))
                {
                    open.Add(new OpenNotificationHandlerDefinition(type));
                    continue;
                }

                foreach (var handlerInterface in interfaces)
                {
                    if (type.Arity != 0 || AssemblyTypes.IsNestedInGenericType(type) ||
                        ContainsTypeParameter(handlerInterface))
                    {
                        continue;
                    }

                    var handler = Create(type, handlerInterface, symbols);
                    if (!CanReference(compilation, handler))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            DiagnosticCatalog.InaccessibleReferencedMessage,
                            Location.None,
                            CSharpNames.Type(handler.MessageType),
                            assembly.Name));
                        continue;
                    }

                    closed.Add(handler);
                }
            }
        }

        return Inventory(closed, open);
    }

    private static HandlerInventory Inventory(
        IEnumerable<HandlerDefinition> closed,
        IEnumerable<OpenNotificationHandlerDefinition> open) =>
        new(
            closed.OrderBy(static handler => handler.SortKey, StringComparer.Ordinal).ToImmutableArray(),
            open.OrderBy(static handler => handler.SortKey, StringComparer.Ordinal).ToImmutableArray());

    private static ImmutableArray<INamedTypeSymbol> HandlerInterfaces(
        INamedTypeSymbol type,
        DispatcherSymbols symbols) =>
        type.AllInterfaces.Where(@interface => symbols.IsHandler(@interface.OriginalDefinition)).ToImmutableArray();

    private static bool IsOpen(INamedTypeSymbol type, ImmutableArray<INamedTypeSymbol> interfaces) =>
        type.Arity != 0 || AssemblyTypes.IsNestedInGenericType(type) || interfaces.Any(ContainsTypeParameter);

    private static void ScanOpenHandler(
        Compilation compilation,
        DispatcherSymbols symbols,
        INamedTypeSymbol type,
        ImmutableArray<INamedTypeSymbol> interfaces,
        ImmutableArray<OpenNotificationHandlerDefinition>.Builder open,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        if (!IsSupportedOpenNotificationHandler(type, interfaces, symbols.NotificationHandler))
        {
            diagnostics.Add(Diagnostic.Create(
                DiagnosticCatalog.UnsupportedGenericHandler,
                type.Locations.FirstOrDefault(),
                CSharpNames.Type(type)));
            return;
        }

        if (compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) && AssemblyTypes.HasPublicConstructor(type))
        {
            open.Add(new OpenNotificationHandlerDefinition(type));
            return;
        }

        diagnostics.Add(Diagnostic.Create(
            DiagnosticCatalog.HandlerCannotBeActivated,
            type.Locations.FirstOrDefault(),
            CSharpNames.Type(type)));
    }

    private static bool IsSupportedOpenNotificationHandler(
        INamedTypeSymbol type,
        ImmutableArray<INamedTypeSymbol> interfaces,
        INamedTypeSymbol notificationHandler)
    {
        if (type is not { TypeKind: TypeKind.Class, IsAbstract: false, Arity: 1 } ||
            interfaces.Length != 1 || AssemblyTypes.IsNestedInGenericType(type))
        {
            return false;
        }

        var handlerInterface = interfaces[0];
        return SymbolEqualityComparer.Default.Equals(handlerInterface.OriginalDefinition, notificationHandler) &&
            SymbolEqualityComparer.Default.Equals(handlerInterface.TypeArguments[0], type.TypeParameters[0]);
    }

    private static bool CanActivate(Compilation compilation, INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Class &&
        !type.IsAbstract &&
        compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) &&
        AssemblyTypes.HasPublicConstructor(type);

    private static bool CanReference(Compilation compilation, HandlerDefinition handler) =>
        compilation.IsSymbolAccessibleWithin(handler.MessageType, compilation.Assembly) &&
        (handler.ResponseType is null || compilation.IsSymbolAccessibleWithin(handler.ResponseType, compilation.Assembly));

    private static HandlerDefinition Create(
        INamedTypeSymbol implementation,
        INamedTypeSymbol handlerInterface,
        DispatcherSymbols symbols) =>
        new(
            Kind(handlerInterface.OriginalDefinition, symbols),
            handlerInterface.TypeArguments[0],
            handlerInterface.TypeArguments.Length == 2 ? handlerInterface.TypeArguments[1] : null,
            implementation);

    private static HandlerKind Kind(INamedTypeSymbol definition, DispatcherSymbols symbols)
    {
        if (SymbolEqualityComparer.Default.Equals(definition, symbols.QueryHandler))
        {
            return HandlerKind.Query;
        }

        if (SymbolEqualityComparer.Default.Equals(definition, symbols.CommandHandler))
        {
            return HandlerKind.CommandWithResponse;
        }

        return SymbolEqualityComparer.Default.Equals(definition, symbols.ResultlessCommandHandler)
            ? HandlerKind.Command
            : HandlerKind.Notification;
    }

    private static bool ContainsTypeParameter(INamedTypeSymbol type) =>
        type.TypeArguments.Any(static argument => argument.TypeKind == TypeKind.TypeParameter);
}