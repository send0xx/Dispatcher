using System.Collections.Immutable;
using Dispatcher.SourceGeneration.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dispatcher.SourceGeneration.Analysis;

internal static class DispatcherAnalyzer
{
    private const string AttributeMetadataName = "Dispatcher.GenerateDispatcherHandlersAttribute";
    private const string DispatcherAttributeMetadataName = "Dispatcher.GenerateDispatcherAttribute";
    private const string QueryHandlerMetadataName = "Dispatcher.IQueryHandler`2";
    private const string CommandHandlerMetadataName = "Dispatcher.ICommandHandler`2";
    private const string CommandWithoutResponseHandlerMetadataName = "Dispatcher.ICommandHandler`1";
    private const string NotificationHandlerMetadataName = "Dispatcher.INotificationHandler`1";
    private const string QueryMetadataName = "Dispatcher.IQuery`1";
    private const string CommandMetadataName = "Dispatcher.ICommand`1";
    private const string CommandWithoutResponseMetadataName = "Dispatcher.ICommand";
    private const string PipelineBehaviorMetadataName = "Dispatcher.IPipelineBehavior`2";
    
    internal static GenerationResult Analyze(Compilation compilation, CancellationToken cancellationToken)
    {
        var attributeType = compilation.GetTypeByMetadataName(AttributeMetadataName);
        var dispatcherAttributeType = compilation.GetTypeByMetadataName(DispatcherAttributeMetadataName);
        if (attributeType is null || dispatcherAttributeType is null)
        {
            return GenerationResult.Empty;
        }
    
        var attribute = compilation.Assembly.GetAttributes()
            .FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, attributeType));
        var dispatcherAttribute = compilation.Assembly.GetAttributes()
            .FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.AttributeClass, dispatcherAttributeType));
        if (attribute is null && dispatcherAttribute is null)
        {
            return GenerationResult.Empty;
        }
    
        var methodName = attribute?.ConstructorArguments.Length == 1
            ? attribute.ConstructorArguments[0].Value as string
            : null;
        var dispatcherMethodName = dispatcherAttribute?.ConstructorArguments.Length == 1
            ? dispatcherAttribute.ConstructorArguments[0].Value as string
            : null;
        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        var attributeLocation = attribute?.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation();
    
        if (attribute is not null && (string.IsNullOrWhiteSpace(methodName) ||
            !SyntaxFacts.IsValidIdentifier(methodName) ||
            SyntaxFacts.GetKeywordKind(methodName) != SyntaxKind.None))
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidMethodName,
                attributeLocation,
                methodName ?? string.Empty));
            return new GenerationResult(
                null,
                dispatcherMethodName,
                ImmutableArray<HandlerModel>.Empty,
                ImmutableArray<HandlerModel>.Empty,
                ImmutableArray<INamedTypeSymbol>.Empty,
                diagnostics.ToImmutable());
        }
    
        if (dispatcherAttribute is not null && (string.IsNullOrWhiteSpace(dispatcherMethodName) ||
            !SyntaxFacts.IsValidIdentifier(dispatcherMethodName) ||
            SyntaxFacts.GetKeywordKind(dispatcherMethodName) != SyntaxKind.None))
        {
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.InvalidDispatcherMethodName,
                dispatcherAttribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation(),
                dispatcherMethodName ?? string.Empty));
            return new GenerationResult(
                methodName,
                null,
                ImmutableArray<HandlerModel>.Empty,
                ImmutableArray<HandlerModel>.Empty,
                ImmutableArray<INamedTypeSymbol>.Empty,
                diagnostics.ToImmutable());
        }
    
        var queryHandler = compilation.GetTypeByMetadataName(QueryHandlerMetadataName);
        var commandHandler = compilation.GetTypeByMetadataName(CommandHandlerMetadataName);
        var commandWithoutResponseHandler = compilation.GetTypeByMetadataName(
            CommandWithoutResponseHandlerMetadataName);
        var notificationHandler = compilation.GetTypeByMetadataName(NotificationHandlerMetadataName);
        if (queryHandler is null || commandHandler is null || commandWithoutResponseHandler is null ||
            notificationHandler is null)
        {
            return new GenerationResult(
                methodName,
                dispatcherMethodName,
                ImmutableArray<HandlerModel>.Empty,
                ImmutableArray<HandlerModel>.Empty,
                ImmutableArray<INamedTypeSymbol>.Empty,
                diagnostics.ToImmutable());
        }
    
        var allTypes = GetAllTypes(compilation.Assembly.GlobalNamespace).ToImmutableArray();
        var pipelineBehavior = compilation.GetTypeByMetadataName(PipelineBehaviorMetadataName);
        var openBehaviors = pipelineBehavior is null
            ? ImmutableArray<INamedTypeSymbol>.Empty
            : GetOpenPipelineBehaviors(allTypes, pipelineBehavior, diagnostics);
        var handlers = ImmutableArray.CreateBuilder<HandlerModel>();
    
        foreach (var type in allTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
    
            var implementedHandlers = type.AllInterfaces
                .Where(@interface => IsHandlerInterface(
                    @interface.OriginalDefinition,
                    queryHandler,
                    commandHandler,
                    commandWithoutResponseHandler,
                    notificationHandler))
                .ToImmutableArray();
            if (implementedHandlers.IsDefaultOrEmpty)
            {
                continue;
            }
    
            if (type.Arity != 0 || implementedHandlers.Any(ContainsTypeParameter))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.OpenGenericHandler,
                    type.Locations.FirstOrDefault(),
                    type.ToDisplayString(SymbolDisplayFormats.FullyQualified)));
                continue;
            }
    
            if (type.TypeKind != TypeKind.Class || type.IsAbstract ||
                !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) ||
                !type.InstanceConstructors.Any(constructor =>
                    !constructor.IsStatic && constructor.DeclaredAccessibility == Accessibility.Public))
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.HandlerCannotBeActivated,
                    type.Locations.FirstOrDefault(),
                    type.ToDisplayString(SymbolDisplayFormats.FullyQualified)));
                continue;
            }
    
            foreach (var handlerInterface in implementedHandlers)
            {
                handlers.Add(CreateHandlerModel(
                    type,
                    handlerInterface,
                    queryHandler,
                    commandHandler,
                    commandWithoutResponseHandler));
            }
        }
    
        AddMissingHandlerDiagnostics(compilation, allTypes, handlers, diagnostics);
    
        var localHandlers = handlers
            .OrderBy(handler => handler.SortKey, StringComparer.Ordinal)
            .ToImmutableArray();
        var dispatchHandlers = ImmutableArray.CreateBuilder<HandlerModel>();
        dispatchHandlers.AddRange(localHandlers);
    
        if (dispatcherAttribute is not null)
        {
            AddReferencedHandlers(
                compilation,
                queryHandler,
                commandHandler,
                commandWithoutResponseHandler,
                notificationHandler,
                dispatchHandlers,
                diagnostics,
                cancellationToken);
            AddDuplicateDiagnostics(dispatchHandlers, diagnostics);
        }
        else
        {
            AddDuplicateDiagnostics(localHandlers, diagnostics);
        }
    
        return new GenerationResult(
            methodName,
            dispatcherMethodName,
            localHandlers,
            dispatchHandlers.OrderBy(handler => handler.SortKey, StringComparer.Ordinal).ToImmutableArray(),
            openBehaviors,
            diagnostics.ToImmutable(),
            compilation.AssemblyName ?? "DispatcherModule");
    }
    
    private static ImmutableArray<INamedTypeSymbol> GetOpenPipelineBehaviors(
        ImmutableArray<INamedTypeSymbol> allTypes,
        INamedTypeSymbol pipelineBehavior,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var behaviors = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var type in allTypes.Where(type => type.TypeKind == TypeKind.Class && !type.IsAbstract && type.Arity > 0))
        {
            var behaviorInterface = type.AllInterfaces.FirstOrDefault(@interface =>
                SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, pipelineBehavior));
            if (behaviorInterface is null)
            {
                continue;
            }
    
            var supported = type.Arity == 2 &&
                SymbolEqualityComparer.Default.Equals(behaviorInterface.TypeArguments[0], type.TypeParameters[0]) &&
                SymbolEqualityComparer.Default.Equals(behaviorInterface.TypeArguments[1], type.TypeParameters[1]) &&
                type.InstanceConstructors.Any(constructor =>
                    !constructor.IsStatic && constructor.DeclaredAccessibility == Accessibility.Public);
            if (!supported)
            {
                diagnostics.Add(Diagnostic.Create(
                    GeneratorDiagnostics.UnsupportedOpenGenericBehavior,
                    type.Locations.FirstOrDefault(),
                    type.ToDisplayString(SymbolDisplayFormats.FullyQualified)));
                continue;
            }
    
            behaviors.Add(type);
        }
    
        return behaviors.OrderBy(type => type.ToDisplayString(SymbolDisplayFormats.FullyQualified), StringComparer.Ordinal)
            .ToImmutableArray();
    }
    
    private static void AddReferencedHandlers(
        Compilation compilation,
        INamedTypeSymbol queryHandler,
        INamedTypeSymbol commandHandler,
        INamedTypeSymbol commandWithoutResponseHandler,
        INamedTypeSymbol notificationHandler,
        ImmutableArray<HandlerModel>.Builder handlers,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols
                     .Where(HasGeneratedHandlerRegistration))
        {
            foreach (var type in GetAllTypes(assembly.GlobalNamespace))
            {
                cancellationToken.ThrowIfCancellationRequested();
    
                foreach (var handlerInterface in type.AllInterfaces.Where(@interface =>
                             IsHandlerInterface(
                                 @interface.OriginalDefinition,
                                 queryHandler,
                                 commandHandler,
                                 commandWithoutResponseHandler,
                                 notificationHandler)))
                {
                    if (type.Arity != 0 || ContainsTypeParameter(handlerInterface))
                    {
                        continue;
                    }
    
                    var model = CreateHandlerModel(
                        type,
                        handlerInterface,
                        queryHandler,
                        commandHandler,
                        commandWithoutResponseHandler);
                    if (!compilation.IsSymbolAccessibleWithin(model.MessageType, compilation.Assembly) ||
                        model.ResponseType is not null &&
                        !compilation.IsSymbolAccessibleWithin(model.ResponseType, compilation.Assembly))
                    {
                        diagnostics.Add(Diagnostic.Create(
                            GeneratorDiagnostics.InaccessibleReferencedMessage,
                            Location.None,
                            model.MessageType.ToDisplayString(SymbolDisplayFormats.FullyQualified),
                            assembly.Name));
                        continue;
                    }
    
                    handlers.Add(model);
                }
            }
        }
    }
    
    private static bool HasGeneratedHandlerRegistration(IAssemblySymbol assembly) =>
        assembly.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() ==
            "Dispatcher.GenerateDispatcherHandlersAttribute");
    
    private static HandlerModel CreateHandlerModel(
        INamedTypeSymbol implementation,
        INamedTypeSymbol handlerInterface,
        INamedTypeSymbol queryHandler,
        INamedTypeSymbol commandHandler,
        INamedTypeSymbol commandWithoutResponseHandler)
    {
        var definition = handlerInterface.OriginalDefinition;
        var kind = SymbolEqualityComparer.Default.Equals(definition, queryHandler)
            ? HandlerModelKind.Query
            : SymbolEqualityComparer.Default.Equals(definition, commandHandler)
                ? HandlerModelKind.CommandWithResponse
                : SymbolEqualityComparer.Default.Equals(definition, commandWithoutResponseHandler)
                    ? HandlerModelKind.Command
                    : HandlerModelKind.Notification;
    
        return new HandlerModel(
            kind,
            handlerInterface.TypeArguments[0],
            handlerInterface.TypeArguments.Length == 2 ? handlerInterface.TypeArguments[1] : null,
            implementation);
    }
    
    private static void AddDuplicateDiagnostics(
        IEnumerable<HandlerModel> handlers,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        foreach (var group in handlers
                     .Where(handler => handler.Kind != HandlerModelKind.Notification)
                     .GroupBy(handler => handler.MessageType, SymbolEqualityComparer.Default)
                     .Where(group => group.Count() > 1))
        {
            var implementations = string.Join(
                ", ",
                group.Select(handler => handler.ImplementationType.ToDisplayString(SymbolDisplayFormats.FullyQualified))
                    .OrderBy(name => name, StringComparer.Ordinal));
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.DuplicateRequestHandler,
                group.Key!.Locations.FirstOrDefault(),
                group.Key.ToDisplayString(SymbolDisplayFormats.FullyQualified),
                implementations));
        }
    }
    
    private static void AddMissingHandlerDiagnostics(
        Compilation compilation,
        ImmutableArray<INamedTypeSymbol> allTypes,
        ImmutableArray<HandlerModel>.Builder handlers,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var query = compilation.GetTypeByMetadataName(QueryMetadataName);
        var command = compilation.GetTypeByMetadataName(CommandMetadataName);
        var commandWithoutResponse = compilation.GetTypeByMetadataName(CommandWithoutResponseMetadataName);
        if (query is null || command is null || commandWithoutResponse is null)
        {
            return;
        }
    
        foreach (var type in allTypes.Where(type =>
                     type.Locations.Any(location => location.IsInSource) &&
                     type.Arity == 0 &&
                     !type.IsAbstract &&
                     IsRequest(type, query, command, commandWithoutResponse)))
        {
            if (handlers.Any(handler => SymbolEqualityComparer.Default.Equals(handler.MessageType, type)))
            {
                continue;
            }
    
            diagnostics.Add(Diagnostic.Create(
                GeneratorDiagnostics.MissingRequestHandler,
                type.Locations.FirstOrDefault(),
                type.ToDisplayString(SymbolDisplayFormats.FullyQualified)));
        }
    }
    
    private static bool IsRequest(
        INamedTypeSymbol type,
        INamedTypeSymbol query,
        INamedTypeSymbol command,
        INamedTypeSymbol commandWithoutResponse) =>
        type.AllInterfaces.Any(@interface =>
            SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, query) ||
            SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, command) ||
            SymbolEqualityComparer.Default.Equals(@interface, commandWithoutResponse));
    
    private static bool IsHandlerInterface(
        INamedTypeSymbol definition,
        INamedTypeSymbol queryHandler,
        INamedTypeSymbol commandHandler,
        INamedTypeSymbol commandWithoutResponseHandler,
        INamedTypeSymbol notificationHandler) =>
        SymbolEqualityComparer.Default.Equals(definition, queryHandler) ||
        SymbolEqualityComparer.Default.Equals(definition, commandHandler) ||
        SymbolEqualityComparer.Default.Equals(definition, commandWithoutResponseHandler) ||
        SymbolEqualityComparer.Default.Equals(definition, notificationHandler);
    
    private static bool ContainsTypeParameter(INamedTypeSymbol type) =>
        type.TypeArguments.Any(argument => argument.TypeKind == TypeKind.TypeParameter);
    
    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol @namespace)
    {
        foreach (var type in @namespace.GetTypeMembers())
        {
            foreach (var nestedType in GetTypeAndNestedTypes(type))
            {
                yield return nestedType;
            }
        }
    
        foreach (var childNamespace in @namespace.GetNamespaceMembers())
        {
            foreach (var type in GetAllTypes(childNamespace))
            {
                yield return type;
            }
        }
    }
    
    private static IEnumerable<INamedTypeSymbol> GetTypeAndNestedTypes(INamedTypeSymbol type)
    {
        yield return type;
    
        foreach (var nestedType in type.GetTypeMembers())
        {
            foreach (var descendant in GetTypeAndNestedTypes(nestedType))
            {
                yield return descendant;
            }
        }
    }
    
}
