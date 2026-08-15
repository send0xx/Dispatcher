using Dispatcher.SourceGeneration.Tests.TestSupport;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Dispatcher.SourceGeneration.Tests.Generation;

public sealed class DispatcherGeneratorModularityTests
{
    [Fact]
    public void Host_dispatcher_composes_handlers_from_two_modules()
    {
        var orders = GeneratorTestHarness.CompileModule(
            """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddOrdersHandlers")]
            namespace Orders;
            public sealed record GetOrder : IQuery<string>;
            public sealed record OrderCreated : INotification;
            internal sealed class GetOrderHandler : IQueryHandler<GetOrder, string>
            {
                public ValueTask<string> HandleAsync(GetOrder query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("order");
            }
            internal sealed class OrderCreatedHandler : INotificationHandler<OrderCreated>
            {
                public ValueTask HandleAsync(OrderCreated notification, CancellationToken cancellationToken) =>
                    ValueTask.CompletedTask;
            }
            """,
            "Orders.Module");
        var stock = GeneratorTestHarness.CompileModule(
            """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            using Orders;
            [assembly: GenerateDispatcherHandlers("AddStockHandlers")]
            namespace Stock;
            public sealed record GetStock : IQuery<int>;
            internal sealed class GetStockHandler : IQueryHandler<GetStock, int>
            {
                public ValueTask<int> HandleAsync(GetStock query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(10);
            }
            internal sealed class ReserveStockHandler : INotificationHandler<OrderCreated>
            {
                public ValueTask HandleAsync(OrderCreated notification, CancellationToken cancellationToken) =>
                    ValueTask.CompletedTask;
            }
            """,
            "Stock.Module",
            [orders]);

        var result = GeneratorTestHarness.Run(
            """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddDispatcher")]
            """,
            additionalReferences: [orders, stock],
            assemblyName: "Sample.Host");

        AssertNoErrors(result);
        Assert.Contains(
            result.OutputCompilation.SourceModule.ReferencedAssemblySymbols,
            assembly => assembly.Name == "Stock.Module" && assembly.GetAttributes().Any());
        var dispatcher = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains("internal sealed class Dispatcher", StringComparison.Ordinal))).ToString();
        Assert.Contains("QueryCore<global::Orders.GetOrder", dispatcher, StringComparison.Ordinal);
        Assert.Contains("QueryCore<global::Stock.GetStock", dispatcher, StringComparison.Ordinal);
        Assert.Equal(1, Count(dispatcher, "NotificationCore<global::Orders.OrderCreated>"));
        Assert.Contains(
            "for (var index = 0; index < handlers.Count; index++)",
            dispatcher,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Host_dispatcher_discovers_routes_from_a_shared_contracts_assembly()
    {
        var contracts = GeneratorTestHarness.CompileModule(
            """
            using Dispatcher;
            namespace Contracts;
            public sealed record SharedQuery : IQuery<string>;
            public abstract record BaseQuery(string Value) : IQuery<string>;
            public sealed record DerivedQuery(string Value) : BaseQuery(Value);
            """,
            "Contracts");
        var handlers = GeneratorTestHarness.CompileModule(
            """
            using Contracts;
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddSharedHandlers")]
            internal sealed class SharedQueryHandler : IQueryHandler<SharedQuery, string>
            {
                public ValueTask<string> HandleAsync(SharedQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("shared");
            }
            internal sealed class BaseQueryHandler : IQueryHandler<BaseQuery, string>
            {
                public ValueTask<string> HandleAsync(BaseQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult(query.Value);
            }
            """,
            "Shared.Handlers",
            [contracts]);

        var result = GeneratorTestHarness.Run(
            """
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddDispatcher")]
            """,
            additionalReferences: [contracts, handlers],
            assemblyName: "Shared.Host");

        AssertNoErrors(result);
        var dispatcher = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains("internal sealed class Dispatcher", StringComparison.Ordinal))).ToString();
        Assert.Contains(
            "[typeof(global::Contracts.SharedQuery)] = (typeof(string)",
            dispatcher,
            StringComparison.Ordinal);
        Assert.Contains(
            "[typeof(global::Contracts.DerivedQuery)] = (typeof(string)",
            dispatcher,
            StringComparison.Ordinal);
        Assert.Contains(
            "QueryCore<global::Contracts.BaseQuery, string>((global::Contracts.BaseQuery)message",
            dispatcher,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_duplicate_request_handlers_across_modules()
    {
        const string contract = """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            namespace Contracts;
            public sealed record SharedQuery : IQuery<string>;
            """;
        var contracts = GeneratorTestHarness.CompileModule(contract, "Contracts");
        var first = CompileQueryModule("First.Module", "AddFirstHandlers", "FirstHandler", contracts);
        var second = CompileQueryModule("Second.Module", "AddSecondHandlers", "SecondHandler", contracts);

        var result = GeneratorTestHarness.Run(
            """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddDispatcher")]
            """,
            additionalReferences: [contracts, first, second],
            assemblyName: "Duplicate.Host");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "DSPG002");
    }

    [Fact]
    public void Reports_inaccessible_message_from_referenced_module()
    {
        var module = GeneratorTestHarness.CompileModule(
            """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddModuleHandlers")]
            internal sealed record HiddenQuery : IQuery<string>;
            internal sealed class HiddenHandler : IQueryHandler<HiddenQuery, string>
            {
                public ValueTask<string> HandleAsync(HiddenQuery query, CancellationToken cancellationToken) =>
                    ValueTask.FromResult("hidden");
            }
            """,
            "Hidden.Module");

        var result = GeneratorTestHarness.Run(
            """
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddDispatcher")]
            """,
            additionalReferences: [module],
            assemblyName: "Hidden.Host");

        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "DSPG007");
    }

    [Fact]
    public void Host_dispatcher_uses_public_generated_invoker_for_internal_open_handler()
    {
        var contracts = GeneratorTestHarness.CompileModule(
            """
            using Dispatcher;
            namespace Contracts;
            public abstract record SharedEvent : INotification;
            public sealed record SharedEventOccurred : SharedEvent;
            """,
            "Open.Contracts");
        var handlers = GeneratorTestHarness.CompileModule(
            """
            using Contracts;
            using Dispatcher;
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcherHandlers("AddOpenHandlers")]
            internal sealed class AuditHandler<TNotification> : INotificationHandler<TNotification>
                where TNotification : SharedEvent
            {
                public ValueTask HandleAsync(
                    TNotification notification,
                    CancellationToken cancellationToken) => ValueTask.CompletedTask;
            }
            """,
            "Open.Handlers",
            [contracts]);

        var result = GeneratorTestHarness.Run(
            """
            using Dispatcher.SourceGeneration;
            [assembly: GenerateDispatcher("AddDispatcher")]
            """,
            additionalReferences: [contracts, handlers],
            assemblyName: "Open.Host");

        AssertNoErrors(result);
        var dispatcher = Assert.Single(result.GeneratedTrees.Where(tree =>
            tree.ToString().Contains("internal sealed class Dispatcher", StringComparison.Ordinal))).ToString();
        Assert.Contains(
            "global::Dispatcher.SourceGeneration.GeneratedHandlerServiceCollectionExtensions_Open_Handlers.InvokeOpenNotificationHandler_",
            dispatcher,
            StringComparison.Ordinal);
        Assert.Contains("OpenNotificationCore<global::Contracts.SharedEventOccurred>", dispatcher, StringComparison.Ordinal);
        Assert.DoesNotContain("global::AuditHandler", dispatcher, StringComparison.Ordinal);
    }

    private static MetadataReference CompileQueryModule(
        string assemblyName,
        string methodName,
        string handlerName,
        MetadataReference contracts) => GeneratorTestHarness.CompileModule(
        $$"""
        using Dispatcher;
        using Dispatcher.SourceGeneration;
        using Contracts;
        [assembly: GenerateDispatcherHandlers("{{methodName}}")]
        internal sealed class {{handlerName}} : IQueryHandler<SharedQuery, string>
        {
            public ValueTask<string> HandleAsync(SharedQuery query, CancellationToken cancellationToken) =>
                ValueTask.FromResult("value");
        }
        """,
        assemblyName,
        [contracts]);

    private static void AssertNoErrors(GeneratorTestResult result)
    {
        Assert.Empty(result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(result.OutputCompilation.GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    private static int Count(string value, string fragment) =>
        value.Split([fragment], StringSplitOptions.None).Length - 1;
}