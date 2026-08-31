using System.Collections.Immutable;
using Dispatcher.SourceGeneration.Diagnostics;
using Dispatcher.SourceGeneration.Discovery;
using Dispatcher.SourceGeneration.Model;
using Dispatcher.SourceGeneration.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dispatcher.SourceGeneration.Analysis;

internal static class DispatchRouteDiscovery
{
    internal static ImmutableArray<DispatchRoute> Discover(
        Compilation compilation,
        DispatcherSymbols symbols,
        ImmutableArray<INamedTypeSymbol> localTypes,
        ImmutableArray<HandlerDefinition> handlers,
        ImmutableArray<OpenNotificationHandlerDefinition> openHandlers,
        RequestRouteResolver resolver,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        if (symbols.Request is null || symbols.Notification is null)
        {
            return [];
        }

        var messages = DiscoverMessages(
            compilation,
            symbols,
            localTypes,
            handlers,
            openHandlers,
            cancellationToken);
        var routes = ImmutableArray.CreateBuilder<DispatchRoute>();
        foreach (var message in messages.OrderBy(CSharpNames.Type, StringComparer.Ordinal))
        {
            var handler = resolver.Resolve(message, diagnostics);
            var compatibleOpenHandlers = IsNotification(message, symbols.Notification)
                ? openHandlers.Where(openHandler => CanClose(compilation, openHandler, message)).ToImmutableArray()
                : [];
            if (handler is null && compatibleOpenHandlers.IsDefaultOrEmpty)
            {
                continue;
            }

            if (!compilation.IsSymbolAccessibleWithin(message, compilation.Assembly))
            {
                diagnostics.Add(Diagnostic.Create(
                    DiagnosticCatalog.InaccessibleReferencedMessage,
                    Location.None,
                    CSharpNames.Type(message),
                    message.ContainingAssembly.Name));
                continue;
            }

            routes.Add(new DispatchRoute(message, handler, compatibleOpenHandlers));
        }

        return routes.ToImmutable();
    }

    private static HashSet<INamedTypeSymbol> DiscoverMessages(
        Compilation compilation,
        DispatcherSymbols symbols,
        ImmutableArray<INamedTypeSymbol> localTypes,
        ImmutableArray<HandlerDefinition> handlers,
        ImmutableArray<OpenNotificationHandlerDefinition> openHandlers,
        CancellationToken cancellationToken)
    {
        var messages = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        AddConcreteMessages(localTypes, symbols, messages);
        var assemblies = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
        assemblies.UnionWith(compilation.SourceModule.ReferencedAssemblySymbols
            .Where(AssemblyTypes.HasGeneratedHandlerRegistration));

        foreach (var handledType in handlers.Select(static handler => handler.MessageType).OfType<INamedTypeSymbol>())
        {
            assemblies.Add(handledType.ContainingAssembly);
            if (IsConcreteMessage(handledType, symbols))
            {
                messages.Add(handledType);
            }
        }

        foreach (var constraintType in openHandlers
                     .SelectMany(static handler => handler.NotificationTypeParameter.ConstraintTypes)
                     .OfType<INamedTypeSymbol>())
        {
            assemblies.Add(constraintType.ContainingAssembly);
        }

        foreach (var assembly in assemblies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!SymbolEqualityComparer.Default.Equals(assembly, compilation.Assembly))
            {
                AddConcreteMessages(AssemblyTypes.Enumerate(assembly), symbols, messages);
            }
        }

        return messages;
    }

    private static void AddConcreteMessages(
        IEnumerable<INamedTypeSymbol> candidates,
        DispatcherSymbols symbols,
        HashSet<INamedTypeSymbol> messages)
    {
        foreach (var candidate in candidates)
        {
            if (IsConcreteMessage(candidate, symbols))
            {
                messages.Add(candidate);
            }
        }
    }

    private static bool IsConcreteMessage(INamedTypeSymbol type, DispatcherSymbols symbols) =>
        type.Arity == 0 && !type.IsAbstract && type.TypeKind is TypeKind.Class or TypeKind.Struct &&
        type.AllInterfaces.Any(@interface =>
            SymbolEqualityComparer.Default.Equals(@interface, symbols.Request) ||
            SymbolEqualityComparer.Default.Equals(@interface, symbols.Notification));

    private static bool IsNotification(INamedTypeSymbol type, INamedTypeSymbol notification) =>
        type.AllInterfaces.Any(@interface => SymbolEqualityComparer.Default.Equals(@interface, notification));

    private static bool CanClose(
        Compilation compilation,
        OpenNotificationHandlerDefinition handler,
        INamedTypeSymbol notification)
    {
        var parameter = handler.NotificationTypeParameter;
        if (parameter.HasUnmanagedTypeConstraint && !notification.IsUnmanagedType ||
            parameter.HasReferenceTypeConstraint && !notification.IsReferenceType ||
            parameter.HasValueTypeConstraint && !notification.IsValueType ||
            parameter.HasConstructorConstraint && !notification.IsValueType &&
            (!notification.InstanceConstructors.Any(static constructor =>
                 constructor.Parameters.IsEmpty && constructor.DeclaredAccessibility == Accessibility.Public) ||
             notification.IsAbstract))
        {
            return false;
        }

        foreach (var constraint in parameter.ConstraintTypes)
        {
            if (!compilation.ClassifyConversion(notification, Substitute(constraint, parameter, notification))
                    .IsImplicit)
            {
                return false;
            }
        }

        return true;
    }

    private static ITypeSymbol Substitute(ITypeSymbol type, ITypeParameterSymbol parameter, ITypeSymbol argument)
    {
        if (SymbolEqualityComparer.Default.Equals(type, parameter))
        {
            return argument;
        }

        if (type is not INamedTypeSymbol { IsGenericType: true } named)
        {
            return type;
        }

        return named.OriginalDefinition.Construct(named.TypeArguments
            .Select(typeArgument => Substitute(typeArgument, parameter, argument))
            .ToArray());
    }
}