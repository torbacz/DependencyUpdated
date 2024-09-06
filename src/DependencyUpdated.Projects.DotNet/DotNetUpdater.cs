using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Core.Models.Enums;
using Microsoft.Extensions.Caching.Memory;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.Xml;
using System.Xml.Linq;
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

    public IReadOnlyCollection<string> GetAllProjectFiles(string searchPath)
    {
        return ValidDotnetPatterns.SelectMany(dotnetPattern =>
            Directory.GetFiles(searchPath, dotnetPattern, SearchOption.AllDirectories)).ToList();
    }

    public IReadOnlyCollection<UpdateResult> HandleProjectUpdate(IReadOnlyCollection<string> fullPath,
        ICollection<DependencyDetails> dependenciesToUpdate)
    {
        return UpdateCsProj(fullPath, dependenciesToUpdate);
    }

    public async Task<ICollection<DependencyDetails>> ExtractAllPackagesThatNeedToBeUpdated(IReadOnlyCollection<string> fullPath, Project projectConfiguration)
    {
        var nugets = ParseCsproj(fullPath);

        var returnList = new List<DependencyDetails>();
        foreach (var nuget in nugets)
        {
            logger.Verbose("Processing {PackageName}:{PackageVersion}", nuget.Name, nuget.Version);
            var latestVersion = await GetLatestVersion(nuget, projectConfiguration);
            if (latestVersion is null)
            {
                logger.Warning("{PacakgeName} unable to find in sources", nuget.Name);
                continue;
            }

            logger.Information("{PacakgeName} new version {Version} available", nuget.Name, latestVersion);
            returnList.Add(nuget with { Version = latestVersion.Version });
        }

        return returnList;
    }

    private static NuGetVersion? GetMaxVersion(IEnumerable<NuGetVersion> versions, Version currentVersion,
        Project projectConfiguration)
    {
        var baseQuery = versions.Where(x => !x.IsPrerelease);
        if (projectConfiguration.Version == VersionUpdateType.Major)
        {
            return baseQuery.Max();
        }

        if (projectConfiguration.Version == VersionUpdateType.Minor)
        {
            return baseQuery.Where(x =>
                x.Version.Major == currentVersion.Major && x.Version.Minor > currentVersion.Minor).Max();
        }

        if (projectConfiguration.Version == VersionUpdateType.Patch)
        {
            return baseQuery.Where(x =>
                x.Version.Major == currentVersion.Major && x.Version.Minor == currentVersion.Minor &&
                x.Version.Build > currentVersion.Build).Max();
        }

        throw new NotSupportedException($"Version configuration {projectConfiguration.Version} is not supported");
    }

    private static HashSet<DependencyDetails> ParseCsproj(IReadOnlyCollection<string> paths)
    {
        return paths.SelectMany(ParseCsproj).ToHashSet();
    }

    private static HashSet<DependencyDetails> ParseCsproj(string path)
    {
        var document = XDocument.Load(path);
        if (document.Root is null)
        {
            throw new InvalidOperationException("Root object is null");
        }

        var packageReferences = document.Root.Elements("ItemGroup")
            .Elements("PackageReference");

        var nugets = new HashSet<DependencyDetails>();
        foreach (var packageReference in packageReferences)
        {
            var includeAttribute = packageReference.Attribute("Include");
            var versionAttribute = packageReference.Attribute("Version");

            if (includeAttribute == null || versionAttribute == null)
            {
                continue;
            }

            var name = includeAttribute.Value;
            var version = NuGetVersion.Parse(versionAttribute.Value).Version;
            nugets.Add(new DependencyDetails(name, version));
        }

        return nugets;
    }

    private async Task<NuGetVersion?> GetLatestVersion(DependencyDetails package, Project projectConfiguration)
    {
        var existsInCache = memoryCache.TryGetValue<NuGetVersion?>(package.Name, out var cachedVersion);
        if (existsInCache)
        {
            return cachedVersion;
        }

        if (!projectConfiguration.DependencyConfigurations.Any())
        {
            throw new InvalidOperationException(
                $"Missing {nameof(projectConfiguration.DependencyConfigurations)} in config.");
        }

        var packageSources = new List<PackageSource>();
        foreach (var projectConfigurationPath in projectConfiguration.DependencyConfigurations)
        {
            if (projectConfigurationPath.StartsWith("http"))
            {
                packageSources.Add(new PackageSource(projectConfigurationPath));
                continue;
            }
            
            var setting = Settings.LoadSpecificSettings(Path.GetDirectoryName(projectConfigurationPath)!,
                Path.GetFileName(projectConfigurationPath));
            var packageSourceProvider = new PackageSourceProvider(setting);
            var sources = packageSourceProvider.LoadPackageSources();
            packageSources.AddRange(sources);
        }

        var providers = Repository.Provider.GetCoreV3();
        var sourceRepositoryProvider =
            new SourceRepositoryProvider(new PackageSourceProvider(NullSettings.Instance, packageSources), providers);
        var repositories = sourceRepositoryProvider.GetRepositories();
        var version = default(NuGetVersion?);
        foreach (var repository in repositories)
        {
            var findPackageByIdResource = await repository.GetResourceAsync<FindPackageByIdResource>();
            var versions = await findPackageByIdResource.GetAllVersionsAsync(
                package.Name,
                new SourceCacheContext(),
                NullLogger.Instance,
                CancellationToken.None);
            var maxVersion = GetMaxVersion(versions, package.Version, projectConfiguration);
            if (version is null || (maxVersion is not null && maxVersion >= version))
            {
                version = maxVersion;
            }
        }

        memoryCache.Set(package.Name, version);
        return version;
    }

    private IReadOnlyCollection<UpdateResult> UpdateCsProj(IReadOnlyCollection<string> fullPaths,
        ICollection<DependencyDetails> packagesToUpdate)
    {
        return fullPaths.SelectMany(x => UpdateCsProj(x, packagesToUpdate)).ToList();
    }

    private IReadOnlyCollection<UpdateResult> UpdateCsProj(string fullPath,
        ICollection<DependencyDetails> packagesToUpdate)
    {
        logger.Information("Updating: {FullPath} project", fullPath);
        var results = new List<UpdateResult>();
        var document = XDocument.Load(fullPath, LoadOptions.PreserveWhitespace);

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

            var oldPackageVersion = versionAttribute.Value;
            versionAttribute.SetValue(newVersion.ToString());
            results.Add(new UpdateResult(packageName, oldPackageVersion, newVersion.ToString()));
        }

        var settings = new XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, };

        if (results.Count == 0)
        {
            return results;
        }

        using var xmlWriter = XmlWriter.Create(fullPath, settings);
        document.Save(xmlWriter);
        return results;
    }
}