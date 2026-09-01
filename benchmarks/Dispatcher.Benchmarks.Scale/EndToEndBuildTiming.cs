using System.Diagnostics;
using System.Text;

namespace Dispatcher.Benchmarks.Scale;

internal static class EndToEndBuildTiming
{
    internal static async Task RunAsync(FixtureSize size)
    {
        var configuration = FixtureConfiguration.Create(size);
        var sources = FixtureSourceBuilder.Generate(configuration);
        var repository = FindRepositoryRoot();
        var workspace = Path.GetFullPath(Path.Combine(
            repository, "..", $".dispatcher-build-timing-{size}-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(workspace);

        try
        {
            var contractsDirectory = Path.Combine(workspace, "Contracts");
            Directory.CreateDirectory(contractsDirectory);
            await File.WriteAllTextAsync(Path.Combine(contractsDirectory, "Contracts.cs"), sources.Contracts);
            await File.WriteAllTextAsync(
                Path.Combine(contractsDirectory, "Contracts.csproj"),
                Project(
                    contractsDirectory,
                    [Path.Combine(repository, "src/Dispatcher.Abstractions/Dispatcher.Abstractions.csproj")]));

            var moduleProjects = new string[configuration.ModuleCount];
            for (var moduleIndex = 0; moduleIndex < configuration.ModuleCount; moduleIndex++)
            {
                var directory = Path.Combine(workspace, $"Module{moduleIndex:00}");
                Directory.CreateDirectory(directory);
                await File.WriteAllTextAsync(Path.Combine(directory, "Module.cs"), sources.Modules[moduleIndex]);
                var projectPath = Path.Combine(directory, $"Module{moduleIndex:00}.csproj");
                await File.WriteAllTextAsync(
                    projectPath,
                    Project(
                        directory,
                        [
                            Path.Combine(contractsDirectory, "Contracts.csproj"),
                            Path.Combine(repository,
                                "src/Dispatcher.SourceGeneration/Dispatcher.SourceGeneration.csproj")
                        ],
                        Path.Combine(
                            repository,
                            "src/Dispatcher.SourceGeneration.Analyzers/Dispatcher.SourceGeneration.Analyzers.csproj")));
                moduleProjects[moduleIndex] = projectPath;
            }

            var hostDirectory = Path.Combine(workspace, "Host");
            Directory.CreateDirectory(hostDirectory);
            var hostSourcePath = Path.Combine(hostDirectory, "Host.cs");
            await File.WriteAllTextAsync(hostSourcePath, sources.Host);
            var hostProject = Path.Combine(hostDirectory, "Host.csproj");
            await File.WriteAllTextAsync(
                hostProject,
                Project(
                    hostDirectory,
                    moduleProjects.Prepend(Path.Combine(contractsDirectory, "Contracts.csproj")),
                    Path.Combine(
                        repository,
                        "src/Dispatcher.SourceGeneration.Analyzers/Dispatcher.SourceGeneration.Analyzers.csproj"),
                    outputType: "Library",
                    addDependencyInjection: true));

            var clean = await MeasureBuildAsync(hostProject);
            await File.AppendAllTextAsync(hostSourcePath, Environment.NewLine + "// incremental change");
            var incremental = await MeasureBuildAsync(hostProject);
            Console.WriteLine($"Fixture: {size} ({configuration.MessageCount:N0} messages, " +
                              $"{configuration.ModuleCount} modules)");
            Console.WriteLine($"Clean build:       {clean.TotalSeconds:F3} s");
            Console.WriteLine($"Incremental build: {incremental.TotalSeconds:F3} s");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static string Project(
        string projectDirectory,
        IEnumerable<string> references,
        string? analyzer = null,
        string outputType = "Library",
        bool addDependencyInjection = false)
    {
        var project = new StringBuilder();
        project.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
        project.AppendLine("  <PropertyGroup>");
        project.AppendLine("    <TargetFramework>net10.0</TargetFramework>");
        project.AppendLine($"    <OutputType>{outputType}</OutputType>");
        project.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        project.AppendLine("    <Nullable>enable</Nullable>");
        project.AppendLine("    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>");
        project.AppendLine("  </PropertyGroup>");
        project.AppendLine("  <ItemGroup>");
        foreach (var reference in references)
        {
            project.AppendLine(
                $"    <ProjectReference Include=\"{Path.GetRelativePath(projectDirectory, reference)}\" />");
        }

        if (analyzer is not null)
        {
            project.AppendLine(
                $"    <ProjectReference Include=\"{Path.GetRelativePath(projectDirectory, analyzer)}\" OutputItemType=\"Analyzer\" ReferenceOutputAssembly=\"false\" />");
        }

        project.AppendLine("  </ItemGroup>");
        if (addDependencyInjection)
        {
            project.AppendLine("  <ItemGroup>");
            project.AppendLine(
                "    <PackageReference Include=\"Microsoft.Extensions.DependencyInjection\" Version=\"10.0.11\" />");
            project.AppendLine("  </ItemGroup>");
        }

        project.AppendLine("</Project>");
        return project.ToString();
    }

    private static async Task<TimeSpan> MeasureBuildAsync(string project)
    {
        var startInfo = new ProcessStartInfo(FindDotnetHost())
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(project);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--nologo");

        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(startInfo) ??
                            throw new InvalidOperationException("Could not start dotnet build.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        stopwatch.Stop();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                await standardOutput + Environment.NewLine + await standardError);
        }

        return stopwatch.Elapsed;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dispatcher.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not find the Dispatcher repository root.");
    }

    private static string FindDotnetHost()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(configured) && Path.IsPathFullyQualified(configured))
        {
            return configured;
        }

        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "")
                 .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.GetFullPath(Path.Combine(directory, "dotnet"));
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not find the dotnet host.");
    }
}