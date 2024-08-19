using System.Xml;
using System.Xml.Linq;
using DependencyUpdated.Core;
using Microsoft.Extensions.Caching.Memory;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using ILogger = Serilog.ILogger;

namespace DependencyUpdated.Projects.DotNet;

internal sealed class DotNetUpdater(ILogger logger, IMemoryCache memoryCache) : IProjectUpdater
{
    private static readonly string[] ValidDotnetPatterns =
    [
        "*.csproj",
        "*.nfproj",
        "directory.build.props"
    ];

    public async Task<IReadOnlyCollection<UpdateResult>> UpdateProject(string searchPath)
    {
        if (!Path.Exists(searchPath))
        {
            throw new FileNotFoundException("Search path not found", searchPath);
        }

        var projectFiles = GetAllProjectFiles(searchPath);
        var allUpdates = new List<UpdateResult>();
        foreach (var project in projectFiles)
        {
            var result = await HandleProjectUpdate(project);
            allUpdates.AddRange(result);
        }
        
        return allUpdates;
    }

    private IEnumerable<string> GetAllProjectFiles(string searchPath)
    {
        return ValidDotnetPatterns.SelectMany(dotnetPattern => Directory.GetFiles(searchPath, dotnetPattern, SearchOption.AllDirectories));
    }

    private async Task<IReadOnlyCollection<UpdateResult>> HandleProjectUpdate(string fullPath)
    {
        logger.Information("Processing: {FullPath} project", fullPath);
        var nugets = ParseCsproj(fullPath);
        var nugetsToUpdate = new Dictionary<string, NuGetVersion>();
        var returnList = new List<UpdateResult>();
        foreach (var nuget in nugets)
        {
            logger.Verbose("Processing {PackageName}:{PackageVersion}", nuget.Key, nuget.Value);
            var latestVersion = await GetLatestVersion(nuget.Key);
            if (latestVersion is null)
            {
                logger.Warning("{PacakgeName} unable to find in sources", nuget.Key);
                continue;
            }

            if (latestVersion.Version > nuget.Value.Version)
            {
                logger.Information("{PacakgeName} new version {Version} available", nuget.Key, latestVersion);
                nugetsToUpdate.Add(nuget.Key, latestVersion);
                returnList.Add(new UpdateResult(nuget.Key, nuget.Value.ToNormalizedString(),
                    latestVersion.ToNormalizedString()));
            }
        }

        UpdateCsProj(fullPath, nugetsToUpdate);
        return returnList;
    }

    private void UpdateCsProj(string fullPath, Dictionary<string, NuGetVersion> nugetsToUpdate)
    {
        var document = XDocument.Load(fullPath);
        if (document.Root is null)
        {
            throw new InvalidOperationException("Root object is null");
        }
        var packageReferences = document.Root.Elements("ItemGroup")
            .Elements("PackageReference");

        foreach (var packageReference in packageReferences)
        {
            var includeAttribute = packageReference.Attribute("Include");
            var versionAttribute = packageReference.Attribute("Version");
            if (includeAttribute is null)
            {
                continue;
            }

            if (versionAttribute is null)
            {
                continue;
            }
            
            var packageName = includeAttribute.Value;
            var exists = nugetsToUpdate.TryGetValue(packageName, out var newVersion);
            if (!exists || newVersion is null)
            {
                continue;
            }
            
            versionAttribute.SetValue(newVersion.ToNormalizedString());
        }

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
        };

        using var xmlWriter = XmlWriter.Create(fullPath, settings);
        document.Save(xmlWriter);
    }

    private async Task<NuGetVersion?> GetLatestVersion(string packageId)
    {
        var existsInCache = memoryCache.TryGetValue<NuGetVersion?>(packageId, out var cachedVersion);
        if (existsInCache)
        {
            return cachedVersion;
        }
        
        var packageSources = new List<PackageSource>
        {
            new("https://api.nuget.org/v3/index.json"),
        };
        var providers = Repository.Provider.GetCoreV3();
        var sourceRepositoryProvider = new SourceRepositoryProvider(new PackageSourceProvider(NullSettings.Instance, packageSources), providers);
        var repositories = sourceRepositoryProvider.GetRepositories();
        var version = default(NuGetVersion?);
        foreach (var repository in repositories)
        {
            var findPackageByIdResource = await repository.GetResourceAsync<FindPackageByIdResource>();
            var versions = await findPackageByIdResource.GetAllVersionsAsync(
                packageId,
                new SourceCacheContext(),
                NullLogger.Instance,
                CancellationToken.None);
            var maxVersion = versions.Where(x => !x.IsPrerelease).Max();
            if (version is null || (maxVersion is not null && maxVersion >= version))
            {
                version = maxVersion;
            }
        }

        memoryCache.Set(packageId, version);
        return version;
    }

    private static Dictionary<string, NuGetVersion> ParseCsproj(string path)
    {
        var document = XDocument.Load(path);
        if (document.Root is null)
        {
            throw new InvalidOperationException("Root object is null");
        }
        var packageReferences = document.Root.Elements("ItemGroup")
            .Elements("PackageReference");

        var nugets = new Dictionary<string, NuGetVersion>();
        foreach (var packageReference in packageReferences)
        {
            var includeAttribute = packageReference.Attribute("Include");
            var versionAttribute = packageReference.Attribute("Version");

            if (includeAttribute != null && versionAttribute != null)
            {
                var version = NuGetVersion.Parse(versionAttribute.Value);
                nugets[includeAttribute.Value] = version;
            }
        }

        return nugets;
    }
}