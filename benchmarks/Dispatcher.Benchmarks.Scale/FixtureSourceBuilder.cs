using System.Text;

namespace Dispatcher.Benchmarks.Scale;

internal static class FixtureSourceBuilder
{
    private const string ContractsSource =
        """
        using Dispatcher;
        namespace ScaleFixture.Contracts;
        public abstract record FixtureQueryBase(int Value) : IQuery<int>;
        public interface IFixtureQuery : IQuery<int> { int Value { get; } }
        public interface IFixtureNotification : INotification { }
        public abstract record ModuleNotificationBase(int Value) : IFixtureNotification;
        public static class FixtureIdentity { public const int Seed = __SEED__; }
        """;

    internal static FixtureSources Generate(FixtureConfiguration configuration)
    {
        var modules = new string[configuration.ModuleCount];
        var messagesPerModule = configuration.MessageCount / configuration.ModuleCount;
        var remainder = configuration.MessageCount % configuration.ModuleCount;
        var messageIndex = 0;
        for (var moduleIndex = 0; moduleIndex < modules.Length; moduleIndex++)
        {
            var count = messagesPerModule + (moduleIndex < remainder ? 1 : 0);
            modules[moduleIndex] = GenerateModule(moduleIndex, messageIndex, count);
            messageIndex += count;
        }

        var contracts = ContractsSource.Replace(
            "__SEED__",
            configuration.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
        return new FixtureSources(contracts, modules, GenerateHost(configuration.ModuleCount));
    }

    private static string GenerateModule(int moduleIndex, int startIndex, int messageCount)
    {
        var moduleName = $"ScaleFixture.Module{moduleIndex:00}";
        var source = new StringBuilder();
        source.AppendLine("using System;");
        source.AppendLine("using System.Threading;");
        source.AppendLine("using System.Threading.Tasks;");
        source.AppendLine("using Dispatcher;");
        source.AppendLine("using Dispatcher.SourceGeneration;");
        source.AppendLine("using ScaleFixture.Contracts;");
        source.AppendLine($"[assembly: GenerateDispatcherHandlers(\"AddFixtureModule{moduleIndex:00}Handlers\")]");
        source.AppendLine($"namespace {moduleName};");

        if (moduleIndex == 0)
        {
            source.AppendLine(
                "internal sealed class BaseQueryHandler : IQueryHandler<FixtureQueryBase, int> { public ValueTask<int> HandleAsync(FixtureQueryBase query, CancellationToken cancellationToken) => ValueTask.FromResult(query.Value + 1); }");
            source.AppendLine(
                "internal sealed class InterfaceQueryHandler : IQueryHandler<IFixtureQuery, int> { public ValueTask<int> HandleAsync(IFixtureQuery query, CancellationToken cancellationToken) => ValueTask.FromResult(query.Value + 1); }");
            source.AppendLine(
                "internal sealed class BaseNotificationHandler : INotificationHandler<ModuleNotificationBase> { public ValueTask HandleAsync(ModuleNotificationBase notification, CancellationToken cancellationToken) => ValueTask.CompletedTask; }");
            source.AppendLine(
                "internal sealed class OpenNotificationHandler<TNotification> : INotificationHandler<TNotification> where TNotification : IFixtureNotification { public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken) => ValueTask.CompletedTask; }");
            source.AppendLine(
                "internal sealed class ConstrainedOpenNotificationHandler<TNotification> : INotificationHandler<TNotification> where TNotification : ModuleNotificationBase { public ValueTask HandleAsync(TNotification notification, CancellationToken cancellationToken) => ValueTask.CompletedTask; }");
        }

        for (var offset = 0; offset < messageCount; offset++)
        {
            var index = startIndex + offset;
            var suffix = index.ToString("D5");
            switch (index % 4)
            {
                case 0:
                    GenerateQuery(source, index, suffix);
                    break;
                case 1:
                    source.AppendLine($"public sealed record Command{suffix}(int Value) : ICommand<int>;");
                    source.AppendLine(
                        $"internal sealed class Command{suffix}Handler : ICommandHandler<Command{suffix}, int> {{ public ValueTask<int> HandleAsync(Command{suffix} command, CancellationToken cancellationToken) => ValueTask.FromResult(command.Value + 2); }}");
                    break;
                case 2:
                    source.AppendLine($"public sealed record Command{suffix}(int Value) : ICommand;");
                    source.AppendLine(
                        $"internal sealed class Command{suffix}Handler : ICommandHandler<Command{suffix}> {{ public ValueTask HandleAsync(Command{suffix} command, CancellationToken cancellationToken) => ValueTask.CompletedTask; }}");
                    break;
                default:
                    GenerateNotification(source, index, suffix);
                    break;
            }
        }

        source.AppendLine(
            "public sealed class FixtureBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest { public ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => next(cancellationToken); }");
        return source.ToString();
    }

    private static void GenerateQuery(StringBuilder source, int index, string suffix)
    {
        if (index == 0)
        {
            source.AppendLine($"public sealed record Query{suffix}(int Value) : FixtureQueryBase(Value);");
            return;
        }

        if (index == 4)
        {
            source.AppendLine($"public sealed record Query{suffix}(int Value) : IFixtureQuery;");
            return;
        }

        source.AppendLine($"public sealed record Query{suffix}(int Value) : IQuery<int>;");
        source.AppendLine(
            $"internal sealed class Query{suffix}Handler : IQueryHandler<Query{suffix}, int> {{ public ValueTask<int> HandleAsync(Query{suffix} query, CancellationToken cancellationToken) => ValueTask.FromResult(query.Value + 1); }}");
    }

    private static void GenerateNotification(StringBuilder source, int index, string suffix)
    {
        source.AppendLine($"public sealed record Notification{suffix}(int Value) : ModuleNotificationBase(Value);");
        if (index != 3)
        {
            source.AppendLine(
                $"internal sealed class Notification{suffix}Handler : INotificationHandler<Notification{suffix}> {{ public ValueTask HandleAsync(Notification{suffix} notification, CancellationToken cancellationToken) => ValueTask.CompletedTask; }}");
        }

        if (index % 20 == 7)
        {
            source.AppendLine(
                $"internal sealed class SecondNotification{suffix}Handler : INotificationHandler<Notification{suffix}> {{ public ValueTask HandleAsync(Notification{suffix} notification, CancellationToken cancellationToken) => ValueTask.CompletedTask; }}");
        }
    }

    private static string GenerateHost(int moduleCount)
    {
        var source = new StringBuilder();
        source.AppendLine("using System;");
        source.AppendLine("using System.Threading;");
        source.AppendLine("using System.Threading.Tasks;");
        source.AppendLine("using Dispatcher;");
        source.AppendLine("using Dispatcher.SourceGeneration;");
        source.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        source.AppendLine("using ScaleFixture.Module00;");
        source.AppendLine("[assembly: GenerateDispatcher(\"AddGeneratedFixtureDispatcher\")]");
        source.AppendLine("namespace ScaleFixture.Host;");
        source.AppendLine(
            "public sealed class HostBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : IRequest { public ValueTask<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken) => next(cancellationToken); }");
        source.AppendLine("public static class FixtureHost");
        source.AppendLine("{");
        source.AppendLine("    public static IServiceProvider BuildGeneratedProvider()");
        source.AppendLine("    {");
        source.AppendLine("        var services = new ServiceCollection();");
        for (var moduleIndex = 0; moduleIndex < moduleCount; moduleIndex++)
        {
            source.AppendLine($"        services.AddFixtureModule{moduleIndex:00}Handlers();");
        }

        source.AppendLine("        services.AddGeneratedFixtureDispatcher();");
        source.AppendLine("        services.AddPipelineBehavior(typeof(HostBehavior<,>));");
        source.AppendLine("        return services.BuildServiceProvider();");
        source.AppendLine("    }");
        source.AppendLine(
            "    public static async ValueTask<int> DispatchSamplesAsync(IDispatcher dispatcher) { var checksum = await dispatcher.QueryAsync(new Query00000(41)); checksum += await dispatcher.ExecuteAsync(new Command00001(40)); await dispatcher.ExecuteAsync(new Command00002(1)); await dispatcher.PublishAsync(new Notification00003(1)); return checksum; }");
        source.AppendLine("}");
        return source.ToString();
    }
}