using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Projects.Npm.Models;
using Refit;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DependencyUpdated.Projects.Npm;

internal sealed class NpmUpdater : IProjectUpdater
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    
    private static readonly string[] ValidNpmPatterns =
    [
        "package.json",
    ];

    public IReadOnlyCollection<string> GetAllProjectFiles(string searchPath)
    {
        return Directory
            .EnumerateFiles(searchPath, "*.*", SearchOption.AllDirectories)
            .Where(file =>
                ValidNpmPatterns.Any(pattern =>
                    string.Equals(Path.GetFileName(file), pattern, StringComparison.OrdinalIgnoreCase)))
            .Where(x => !x.Contains("node_modules"))
            .Where(x => !x.Contains("dist"))
            .Distinct()
            .ToList();
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
                if (projectDeps.All(x => x.Name != dependency.Name))
                {
                    continue;
                }
                
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
        if (!projectConfiguration.DependencyConfigurations.Any())
        {
            throw new InvalidOperationException(
                $"Missing {nameof(projectConfiguration.DependencyConfigurations)} in config.");
        }

        var data = new List<DependencyDetails>();
        foreach (var depsConfiguration in projectConfiguration.DependencyConfigurations)
        {
            try
            {
                if (depsConfiguration.StartsWith("http"))
                {
                    var npmApi = RestService.For<INpmApi>(depsConfiguration);
                    var packages = await npmApi.GetPackageData(package.Name, null);
                    data.AddRange(packages.Versions.Where(x => IsValidVersion(x.Value.Version))
                        .Select(x => new DependencyDetails(x.Key, new Version(x.Value.Version))));
                    continue;
                }

                if (depsConfiguration.EndsWith(".npmrc"))
                {
                    var allText = await File.ReadAllTextAsync(depsConfiguration);
                    var registryUrlPattern = @"@as:registry=(https\S+)";
                    var usernamePattern = @"username=(\S+)";
                    var passwordPattern = @":_password=(\S+)";
                    var registryUrl = Regex.Match(allText, registryUrlPattern).Groups[1].Value;
                    var username = Regex.Match(allText, usernamePattern).Groups[1].Value;
                    var base64Password = Regex.Match(allText, passwordPattern).Groups[1].Value;
                    var passwordBytes = Convert.FromBase64String(base64Password);
                    var password = Encoding.UTF8.GetString(passwordBytes);
                    var credentials = $"{username}:{password}";
                    var credentialsBase64 = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
                    var npmApi = RestService.For<INpmApi>(registryUrl);
                    var packages = await npmApi.GetPackageData(package.Name, $"Basic {credentialsBase64}");
                    data.AddRange(packages.Versions.Where(x => IsValidVersion(x.Value.Version))
                        .Select(x => new DependencyDetails(x.Key, new Version(x.Value.Version))));
                    continue;
                }
            }
            catch (ApiException ex)
                when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Package not found in source
                continue;
            }

            throw new NotSupportedException($"{depsConfiguration} is not supported");
        }

        return data;
    }

    private static Process GetProcess(string? directory, DependencyDetails dependency)
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform
                .Windows))
        {
            return ProcessPackageWindows(directory, dependency);
        }

        return ProcessPackageGeneric(directory, dependency);
    }

    private static Process ProcessPackageGeneric(string? directory, DependencyDetails dependency)
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
    
    private static Process ProcessPackageWindows(string? directory, DependencyDetails dependency)
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

        var dependencies = ConvertToDependencies(package.Dependencies);
        var devDependencies = ConvertToDependencies(package.DevDependencies); 
        return dependencies.Concat(devDependencies).ToHashSet();
    }

    private static ICollection<DependencyDetails> ConvertToDependencies(Dictionary<string, string>? dependencies)
    {
        if (dependencies is null)
        {
            return Array.Empty<DependencyDetails>();
        }

        if (dependencies.Count == 0)
        {
            return Array.Empty<DependencyDetails>();
        }

        return dependencies
            .Where(x => IsValidVersion(x.Value))
            .Select(d => new DependencyDetails(d.Key, ParseVersion(d.Value))).ToList();
    }

    private static Version ParseVersion(string data)
    {
        return new Version(data.TrimStart('^', '~'));
    }

    private static bool IsValidVersion(string data)
    {
        if (data.Contains('-'))
        {
            return false;
        }

        if (data.Contains('~') && !data.StartsWith('~'))
        {
            return false;
        }

        return true;
    }
    
    private static bool IsNpmInstalled()
    {
        try
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform
                    .Windows))
            {
                IsNpmInstalledWindows();
                return true;
            }

            IsNpmInstalledGeneric();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void IsNpmInstalledWindows()
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
    }
    
    private static void IsNpmInstalledGeneric()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "npm",
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            Arguments = "-v",
        };

        var process = new Process { StartInfo = psi };
        process.Start();
        process.WaitForExit();
    }
}