using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Projects.Npm.Models;
using System.Text.Json;

namespace DependencyUpdated.Projects.Npm;

internal sealed class NpmUpdater : IProjectUpdater
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
        throw new NotImplementedException();
    }

    public async Task<ICollection<DependencyDetails>> ExtractAllPackages(IReadOnlyCollection<string> fullPath)
    {
        return await Task.FromResult(fullPath.SelectMany(ParseProject).ToHashSet());
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

    public async Task<IReadOnlyCollection<DependencyDetails>> GetVersions(DependencyDetails package,
        Project projectConfiguration)
    {
      // TODO: Check new versions
      throw new NotImplementedException();
    }
}