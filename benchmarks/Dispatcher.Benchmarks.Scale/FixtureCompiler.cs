using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Dispatcher.DependencyInjection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.Benchmarks.Scale;

internal sealed record GenerationRun(
    GeneratorDriver Driver,
    Compilation OutputCompilation,
    GeneratorDriverRunResult Result);

internal sealed class FixtureCorpus : IDisposable
{
    private FixtureLoadContext? _loadContext;
    private Func<IServiceProvider>? _buildGeneratedProvider;
    private Func<IDispatcher, ValueTask<int>>? _dispatchSamples;

    internal required FixtureConfiguration Configuration { get; init; }

    internal required ImmutableArray<SyntaxTree> ModuleTrees { get; init; }

    internal required ImmutableArray<CSharpCompilation> ModuleCompilations { get; init; }

    internal required CSharpCompilation HostCompilation { get; init; }

    internal required CSharpCompilation ChangedHostCompilation { get; init; }

    internal required GeneratorDriver CachedHostDriver { get; init; }

    internal required GeneratorDriver ChangedHostBaseDriver { get; init; }

    internal required GeneratorDriver ModuleReferenceBaseDriver { get; init; }

    internal required string TemporaryDirectory { get; init; }

    internal required ImmutableArray<string> ModuleAssemblyPaths { get; init; }

    internal required ImmutableArray<Assembly> LoadedModules { get; set; }

    internal required Assembly LoadedHost { get; set; }

    internal bool LoadContextUnloaded { get; private set; }

    internal static FixtureCorpus Create(FixtureSize size) =>
        FixtureCompiler.Compile(FixtureConfiguration.Create(size));

    internal void AttachRuntime(
        FixtureLoadContext loadContext,
        Func<IServiceProvider> buildGeneratedProvider,
        Func<IDispatcher, ValueTask<int>> dispatchSamples)
    {
        _loadContext = loadContext;
        _buildGeneratedProvider = buildGeneratedProvider;
        _dispatchSamples = dispatchSamples;
    }

    internal ServiceProvider BuildReflectionProvider()
    {
        var services = new ServiceCollection();
        foreach (var module in LoadedModules)
        {
            services.AddDispatcherHandlers(module);
        }

        services.AddDispatcher();
        var behavior = LoadedHost.GetType("ScaleFixture.Host.HostBehavior`2", throwOnError: true)!;
        services.AddPipelineBehavior(behavior);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    internal IServiceProvider BuildGeneratedProvider() => _buildGeneratedProvider!();

    internal ValueTask<int> DispatchSamplesAsync(IDispatcher dispatcher) => _dispatchSamples!(dispatcher);

    public void Dispose()
    {
        _buildGeneratedProvider = null;
        _dispatchSamples = null;
        LoadedModules = [];
        LoadedHost = null!;
        if (_loadContext is not null)
        {
            var weakReference = BeginUnload(_loadContext);
            _loadContext = null;
            LoadContextUnloaded = WaitForUnload(weakReference);
        }

        Directory.Delete(TemporaryDirectory, recursive: true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference BeginUnload(FixtureLoadContext context)
    {
        var weakReference = new WeakReference(context);
        context.Unload();
        return weakReference;
    }

#pragma warning disable S1215 // Forced collections are required to validate collectible fixture unloading.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool WaitForUnload(WeakReference weakReference)
    {
        for (var attempt = 0; weakReference.IsAlive && attempt < 10; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        return !weakReference.IsAlive;
    }
#pragma warning restore S1215
}

internal static class FixtureCompiler
{
    private static readonly CSharpParseOptions ParseOptions =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    internal static FixtureCorpus Compile(FixtureConfiguration configuration)
    {
        var sources = FixtureSourceBuilder.Generate(configuration);
        var duplicate = FixtureSourceBuilder.Generate(configuration);
        if (!sources.All.SequenceEqual(duplicate.All, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Scale fixture generation is not deterministic.");
        }

        if (sources.Modules.Length != configuration.ModuleCount ||
            sources.Modules.Sum(CountMessages) != configuration.MessageCount)
        {
            throw new InvalidOperationException("Scale fixture counts do not match the selected configuration.");
        }

        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(), $"dispatcher-scale-{configuration.Size}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var references = GetBaseReferences();
            var contractsTree = Parse(sources.Contracts, "Contracts.cs");
            var contractsCompilation = CreateCompilation(
                "ScaleFixture.Contracts", [contractsTree], references);
            var contractsPath = Path.Combine(temporaryDirectory, "ScaleFixture.Contracts.dll");
            Emit(contractsCompilation, contractsPath);
            var contractsReference = MetadataReference.CreateFromFile(contractsPath);

            var moduleTrees = ImmutableArray.CreateBuilder<SyntaxTree>(configuration.ModuleCount);
            var moduleCompilations = ImmutableArray.CreateBuilder<CSharpCompilation>(configuration.ModuleCount);
            var modulePaths = ImmutableArray.CreateBuilder<string>(configuration.ModuleCount);
            var moduleReferences = ImmutableArray.CreateBuilder<MetadataReference>(configuration.ModuleCount);
            for (var moduleIndex = 0; moduleIndex < configuration.ModuleCount; moduleIndex++)
            {
                var tree = Parse(sources.Modules[moduleIndex], $"Module{moduleIndex:00}.cs");
                var compilation = CreateCompilation(
                    $"ScaleFixture.Module{moduleIndex:00}",
                    [tree],
                    references.Add(contractsReference));
                var generated = RunGenerator(compilation);
                ValidateGeneration(generated);
                var path = Path.Combine(temporaryDirectory, $"ScaleFixture.Module{moduleIndex:00}.dll");
                Emit(generated.OutputCompilation, path);
                moduleTrees.Add(tree);
                moduleCompilations.Add(compilation);
                modulePaths.Add(path);
                moduleReferences.Add(MetadataReference.CreateFromFile(path));
            }

            var hostTree = Parse(sources.Host, "Host.cs");
            var hostReferences = references.Add(contractsReference).AddRange(moduleReferences);
            var hostCompilation = CreateCompilation("ScaleFixture.Host", [hostTree], hostReferences);
            var generatedHost = RunGenerator(hostCompilation);
            ValidateGeneration(generatedHost);
            var hostPath = Path.Combine(temporaryDirectory, "ScaleFixture.Host.dll");
            Emit(generatedHost.OutputCompilation, hostPath);

            var changedTree = hostTree.WithChangedText(SourceText.From(
                sources.Host + Environment.NewLine +
                "public sealed record IncrementalNotification : global::Dispatcher.INotification;"));
            var changedHost = hostCompilation.ReplaceSyntaxTree(hostTree, changedTree);
            var cachedHost = RunGenerator(hostCompilation);
            var moduleReferenceBaseline = hostCompilation.RemoveReferences(moduleReferences[^1]);
            var moduleReferenceDriver = RunGenerator(moduleReferenceBaseline);

            var loadContext = new FixtureLoadContext(temporaryDirectory);
            _ = loadContext.LoadFromAssemblyPath(contractsPath);
            var loadedModules = modulePaths
                .Select(loadContext.LoadFromAssemblyPath)
                .ToImmutableArray();
            var loadedHost = loadContext.LoadFromAssemblyPath(hostPath);
            var hostType = loadedHost.GetType("ScaleFixture.Host.FixtureHost", throwOnError: true)!;
            var buildProvider = hostType.GetMethod(
                    "BuildGeneratedProvider", BindingFlags.Public | BindingFlags.Static)!
                .CreateDelegate<Func<IServiceProvider>>();
            var dispatchSamples = hostType.GetMethod(
                    "DispatchSamplesAsync", BindingFlags.Public | BindingFlags.Static)!
                .CreateDelegate<Func<IDispatcher, ValueTask<int>>>();

            var corpus = new FixtureCorpus
            {
                Configuration = configuration,
                ModuleTrees = moduleTrees.ToImmutable(),
                ModuleCompilations = moduleCompilations.ToImmutable(),
                HostCompilation = hostCompilation,
                ChangedHostCompilation = changedHost,
                CachedHostDriver = cachedHost.Driver,
                ChangedHostBaseDriver = cachedHost.Driver,
                ModuleReferenceBaseDriver = moduleReferenceDriver.Driver,
                TemporaryDirectory = temporaryDirectory,
                ModuleAssemblyPaths = modulePaths.ToImmutable(),
                LoadedModules = loadedModules,
                LoadedHost = loadedHost
            };
            corpus.AttachRuntime(loadContext, buildProvider, dispatchSamples);
            ValidateRuntimeAsync(corpus).GetAwaiter().GetResult();
            return corpus;
        }
        catch
        {
            Directory.Delete(temporaryDirectory, recursive: true);
            throw;
        }
    }

    internal static GenerationRun RunGenerator(CSharpCompilation compilation)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new SourceGeneration.DispatcherGenerator())
            .WithUpdatedParseOptions(ParseOptions);
        return RunGenerator(driver, compilation);
    }

    internal static GenerationRun RunGenerator(GeneratorDriver driver, CSharpCompilation compilation)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var diagnostics);
        var result = driver.GetRunResult();
        var combinedDiagnostics = diagnostics.AddRange(result.Diagnostics);
        if (combinedDiagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            throw new InvalidOperationException(FormatDiagnostics(combinedDiagnostics));
        }

        return new GenerationRun(driver, outputCompilation, result);
    }

    internal static long EmitToMemory(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            throw new InvalidOperationException(FormatDiagnostics(result.Diagnostics));
        }

        return stream.Length;
    }

    private static SyntaxTree Parse(string source, string path) =>
        CSharpSyntaxTree.ParseText(source, ParseOptions, path);

    private static CSharpCompilation CreateCompilation(
        string assemblyName,
        IEnumerable<SyntaxTree> syntaxTrees,
        IEnumerable<MetadataReference> references) =>
        CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable,
                deterministic: true));

    private static ImmutableArray<MetadataReference> GetBaseReferences()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Concat(
            [
                typeof(IRequest).Assembly.Location,
                typeof(DispatcherOptions).Assembly.Location,
                typeof(IServiceCollection).Assembly.Location,
                typeof(ServiceCollection).Assembly.Location
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return paths.Select(static path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private static void Emit(Compilation compilation, string path)
    {
        var result = compilation.Emit(path);
        if (!result.Success)
        {
            throw new InvalidOperationException(FormatDiagnostics(result.Diagnostics));
        }
    }

    private static void ValidateGeneration(GenerationRun generation)
    {
        var diagnostics = generation.OutputCompilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
        if (!diagnostics.IsEmpty)
        {
            throw new InvalidOperationException(FormatDiagnostics(diagnostics));
        }
    }

    private static async Task ValidateRuntimeAsync(FixtureCorpus corpus)
    {
        using var reflectionProvider = corpus.BuildReflectionProvider();
        using var reflectionScope = reflectionProvider.CreateScope();
        var reflectionDispatcher = reflectionScope.ServiceProvider.GetRequiredService<IDispatcher>();
        using var generatedProvider = (ServiceProvider)corpus.BuildGeneratedProvider();
        using var generatedScope = generatedProvider.CreateScope();
        var generatedDispatcher = generatedScope.ServiceProvider.GetRequiredService<IDispatcher>();

        var reflectionResult = await corpus.DispatchSamplesAsync(reflectionDispatcher);
        var generatedResult = await corpus.DispatchSamplesAsync(generatedDispatcher);
        if (reflectionResult != 84 || generatedResult != reflectionResult)
        {
            throw new InvalidOperationException("Reflection and generated scale fixtures returned different results.");
        }
    }

    private static int CountMessages(string source)
    {
        const string marker = "public sealed record ";
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(marker, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += marker.Length;
        }

        return count;
    }

    private static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics) =>
        string.Join(Environment.NewLine, diagnostics.Select(static diagnostic => diagnostic.ToString()));
}

internal sealed class FixtureLoadContext(string directory)
    : AssemblyLoadContext(isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is null ||
            assemblyName.Name.StartsWith("Dispatcher", StringComparison.Ordinal) ||
            assemblyName.Name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal))
        {
            return null;
        }

        var path = Path.Combine(directory, assemblyName.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
}