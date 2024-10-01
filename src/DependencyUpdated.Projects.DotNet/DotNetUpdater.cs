using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using Microsoft.Extensions.Caching.Memory;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.Net;
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

    public async Task<ICollection<DependencyDetails>> ExtractAllPackages(IReadOnlyCollection<string> fullPath)
    {
        return await Task.FromResult(ParseCsproj(fullPath));
    }

    public async Task<IReadOnlyCollection<DependencyDetails>> GetVersions(DependencyDetails package,
        Project projectConfiguration)
    {
        var existsInCache =
            memoryCache.TryGetValue<IReadOnlyCollection<DependencyDetails>>(package.Name, out var cachedVersion);
        if (existsInCache && cachedVersion is not null)
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
        var allVersions = new List<NuGetVersion>();
        foreach (var repository in repositories)
        {
            var findPackageByIdResource = await repository.GetResourceAsync<FindPackageByIdResource>();
            try
            {
                var versions = await findPackageByIdResource.GetAllVersionsAsync(
                    package.Name,
                    new SourceCacheContext(),
                    NullLogger.Instance,
                    CancellationToken.None);
                allVersions.AddRange(versions.Where(x => !x.IsPrerelease));
            }
            catch (FatalProtocolException ex)
                when (ex.InnerException is HttpRequestException { StatusCode: HttpStatusCode.NotFound })
            {
                // Package not found in source
            }
        }

        var result = allVersions
            .DistinctBy(x => x.Version)
            .Select(x => package with { Version = x.Version })
            .ToHashSet();
        memoryCache.Set(package.Name, result);
        return result;
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