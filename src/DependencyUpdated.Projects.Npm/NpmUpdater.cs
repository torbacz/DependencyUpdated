using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Projects.Npm.Models;
using Refit;
using Serilog;
using System.Diagnostics;
using System.Text.Json;

namespace DependencyUpdated.Projects.Npm;

internal sealed class NpmUpdater(ILogger logger) : IProjectUpdater
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    private static readonly string[] ValidNpmPatterns =
    [
        "packages.json",
    ];
    
    public IReadOnlyCollection<string> GetAllProjectFiles(string searchPath)
    {
        return ValidNpmPatterns.SelectMany(dotnetPattern =>
            Directory.GetFiles(searchPath, dotnetPattern, SearchOption.AllDirectories)).ToList();
    }

    public IReadOnlyCollection<UpdateResult> HandleProjectUpdate(IReadOnlyCollection<string> fullPath,
        ICollection<DependencyDetails> dependenciesToUpdate)
    {
        if (!IsNpmInstalled())
        {
            throw new InvalidOperationException("Npm is not installed");
        }

        var updates = new List<UpdateResult>();
        foreach (var path in fullPath)
        {
            var absolutePath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(absolutePath);
            var projectDeps = ParseProject(path);
            foreach (var dependency in dependenciesToUpdate)
            {
                var process = GetProcess(directory, dependency);
                process.WaitForExit();
                var oldDep = projectDeps.First(x => x.Name == dependency.Name);
                updates.Add(new UpdateResult(dependency.Name, oldDep.Version.ToString(), dependency.Version.ToString()));

                if (process.ExitCode != 0)
                {
                    var result = process.StandardOutput.ReadToEnd();
                    throw new InvalidOperationException($"Unable to update: {result}");
                }
            }
        }

        return updates;
    }
    
    public async Task<ICollection<DependencyDetails>> ExtractAllPackages(IReadOnlyCollection<string> fullPath)
    {
        return await Task.FromResult(fullPath.SelectMany(ParseProject).ToHashSet());
    }
    
    public async Task<IReadOnlyCollection<DependencyDetails>> GetVersions(DependencyDetails package,
        Project projectConfiguration)
    {
        #pragma warning disable S1075
        var npmApi = RestService.For<INpmApi>("https://registry.npmjs.org");
        var data = await npmApi.GetPackageData(package.Name);
        return data.Versions.Where(x => IsValidVersion(x.Value.Version))
            .Select(x => new DependencyDetails(x.Key, new Version(x.Value.Version))).ToList();
    }

    private static Process GetProcess(string? directory, DependencyDetails dependency)
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform
                .Windows))
        {
            return ProcessPacakgeWindows(directory, dependency);
        }

        return ProcessPacakgeGeneric(directory, dependency);
    }

    private static Process ProcessPacakgeGeneric(string? directory, DependencyDetails dependency)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "npm",
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            Arguments = $"install {dependency.Name}@{dependency.Version}",
            WorkingDirectory = directory
        };

        var process = new Process { StartInfo = psi };
        process.Start();
        return process;
    }
    
    private static Process ProcessPacakgeWindows(string? directory, DependencyDetails dependency)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            WorkingDirectory = directory
        };

        var process = new Process { StartInfo = psi };
        process.Start();
        using var sw = process.StandardInput;
        sw.WriteLine($"npm install {dependency.Name}@{dependency.Version}");
        sw.WriteLine("exit");
        return process;
    }

    private static HashSet<DependencyDetails> ParseProject(string path)
    {
        var json = File.ReadAllText(path);
        var package = JsonSerializer.Deserialize<PackageRoot>(json, JsonSerializerOptions);
        
        if (package is null)
        {
            return new HashSet<DependencyDetails>();
        }

        var dependencies = package.Dependencies
            .Select(d => new DependencyDetails(d.Key, ParseVersion(d.Value))).ToList();
        var devDependencies = package.DevDependencies
            .Select(d => new DependencyDetails(d.Key, ParseVersion(d.Value))).ToList();
        return dependencies.Concat(devDependencies).ToHashSet();
    }

    private static Version ParseVersion(string data)
    {
        return new Version(data.TrimStart('^').TrimStart('~'));
    }

    private static bool IsValidVersion(string data)
    {
        if (data.Contains('-'))
        {
            return false;
        }

        return true;
    }
    
    private bool IsNpmInstalled()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true
            };

            var process = new Process { StartInfo = psi };
            process.Start();
            using var sw = process.StandardInput;
            sw.WriteLine("npm -v");
            sw.WriteLine("exit");
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            return false;
        }
    }
}