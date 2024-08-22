using System.Xml;
using System.Xml.Linq;
using DependencyUpdated.Core;
using DependencyUpdated.Core.Config;
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
    
    public IEnumerable<string> GetAllProjectFiles(string searchPath)
    {
        return ValidDotnetPatterns.SelectMany(dotnetPattern =>
            Directory.GetFiles(searchPath, dotnetPattern, SearchOption.AllDirectories));
    }

    public IReadOnlyCollection<UpdateResult> HandleProjectUpdate(string fullPath, ICollection<DependencyDetails> dependenciesToUpdate)
    {
        logger.Information("Processing: {FullPath} project", fullPath);
        return UpdateCsProj(fullPath, dependenciesToUpdate);
    }

    public async Task<ICollection<DependencyDetails>> ExtractAllPackagesThatNeedToBeUpdated(string fullPath, Project projectConfiguration)
    {
        var nugets = ParseCsproj(fullPath);

        var returnList = new List<DependencyDetails>();
        foreach (var nuget in nugets)
        {
            logger.Verbose("Processing {PackageName}:{PackageVersion}", nuget.Key, nuget.Value);
            var latestVersion = await GetLatestVersion(nuget.Key, projectConfiguration);
            if (latestVersion is null)
            {
                logger.Warning("{PacakgeName} unable to find in sources", nuget.Key);
                continue;
            }

            if (latestVersion.Version > nuget.Value.Version)
            {
                logger.Information("{PacakgeName} new version {Version} available", nuget.Key, latestVersion);
                returnList.Add(new DependencyDetails(nuget.Key, latestVersion.Version));
            }
        }

        return returnList;
    }

    private IReadOnlyCollection<UpdateResult> UpdateCsProj(string fullPath, ICollection<DependencyDetails> packagesToUpdate)
    {
        var results = new List<UpdateResult>();
        var document = XDocument.Load(fullPath);

        var nugetsToUpdate = packagesToUpdate.ToDictionary(x => x.Name, x => x.Version);
        
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
            if (includeAttribute is null || versionAttribute is null)
            {
                continue;
            }
            
            var packageName = includeAttribute.Value;
            var exists = nugetsToUpdate.TryGetValue(packageName, out var newVersion);
            if (!exists || newVersion is null)
            {
                continue;
            }
            
            versionAttribute.SetValue(newVersion.ToString());
            results.Add(new UpdateResult(packageName, versionAttribute.Value, newVersion.ToString()));
        }

        var settings = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
        };

        using var xmlWriter = XmlWriter.Create(fullPath, settings);
        document.Save(xmlWriter);

        return results;
    }

    private async Task<NuGetVersion?> GetLatestVersion(string packageId, Project projectConfiguration)
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

        if (projectConfiguration.DependencyConfigurations.Any())
        {
            packageSources = new List<PackageSource>();
            foreach (var projectConfigurationPath in projectConfiguration.DependencyConfigurations)
            {
                var setting = Settings.LoadSpecificSettings(Path.GetDirectoryName(projectConfigurationPath)!,
                    Path.GetFileName(projectConfigurationPath));
                var packageSourceProvider = new PackageSourceProvider(setting);
                var sources = packageSourceProvider.LoadPackageSources();
                packageSources.AddRange(sources);
            }
        }
        
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

    private static Dictionary<string, DependencyDetails> ParseCsproj(string path)
    {
        var document = XDocument.Load(path);
        if (document.Root is null)
        {
            throw new InvalidOperationException("Root object is null");
        }
        var packageReferences = document.Root.Elements("ItemGroup")
            .Elements("PackageReference");

        var nugets = new Dictionary<string, DependencyDetails>();
        foreach (var packageReference in packageReferences)
        {
            var includeAttribute = packageReference.Attribute("Include");
            var versionAttribute = packageReference.Attribute("Version");

            if (includeAttribute != null && versionAttribute != null)
            {
                var version = NuGetVersion.Parse(versionAttribute.Value).Version;
                nugets[includeAttribute.Value] =
                    new DependencyDetails(includeAttribute.Value, version);
            }
        }

        return nugets;
    }
}