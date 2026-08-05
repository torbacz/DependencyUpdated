using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Core.Models.Enums;
using Microsoft.Extensions.Caching.Memory;
using Serilog;
using System.IO.Enumeration;

namespace DependencyUpdated.Core;

public sealed class DependencyUpdateProcessor(
    IProjectUpdater projectUpdater,
    IRepositoryProvider repositoryProvider,
    ILogger logger,
    IMemoryCache memoryCache)
{
    private readonly List<string> cacheKeys = new();

    public async Task<IReadOnlyCollection<UpdateResult>?> ProcessDirectoryUpdate(
        Project project,
        IReadOnlyCollection<string> projectFiles,
        ICollection<DependencyDetails> allProjectDependencies,
        ICollection<DependencyDetails> alreadyProcessed,
        string projectName,
        string group,
        string repositoryPath)
    {
        var filteredPackages = FilterPackages(allProjectDependencies, alreadyProcessed, group, project);
        if (filteredPackages.Count == 0)
        {
            return null;
        }

        logger.Debug("Filtered packages {Packages}", filteredPackages);
        foreach (var package in filteredPackages)
        {
            alreadyProcessed.Add(package);
        }

        var dependenciesToUpdate = await GetLatestVersions(filteredPackages, project);
        if (dependenciesToUpdate.Count == 0)
        {
            return null;
        }

        logger.Verbose("Found new versions: {Packages}", dependenciesToUpdate);
        var updates = projectUpdater.HandleProjectUpdate(project, projectFiles, dependenciesToUpdate);
        if (updates.Count == 0)
        {
            return null;
        }

        logger.Information("Updated packages {Packages}", updates);
        if (!repositoryProvider.CommitChanges(repositoryPath, projectName, group))
        {
            logger.Debug("No changes detected. Skipping pull request");
            return null;
        }

        return updates;
    }

    public void ClearCache()
    {
        foreach (var key in cacheKeys)
        {
            memoryCache.Remove(key);
        }

        cacheKeys.Clear();
    }

    private static ICollection<DependencyDetails> FilterPackages(
        ICollection<DependencyDetails> allPackagesFromProjects,
        ICollection<DependencyDetails> alreadyProcessed,
        string group,
        Project project)
    {
        if (allPackagesFromProjects.Count == 0)
        {
            return ArraySegment<DependencyDetails>.Empty;
        }

        var basePackages = allPackagesFromProjects.ExceptBy(alreadyProcessed.Select(x => x.Name), x => x.Name);
        if (project.Include.Count > 0)
        {
            basePackages = basePackages.Where(x => project.Include.Any(include =>
                FileSystemName.MatchesSimpleExpression(include, x.Name)));
        }

        if (project.Exclude.Count > 0)
        {
            basePackages = basePackages.Where(x => !project.Exclude.Any(exclude =>
                FileSystemName.MatchesSimpleExpression(exclude, x.Name)));
        }

        return basePackages
            .Where(x => FileSystemName.MatchesSimpleExpression(group, x.Name))
            .ToArray();
    }

    private static DependencyDetails? GetMaxVersion(
        IReadOnlyCollection<DependencyDetails> versions,
        Version currentVersion,
        Project project)
    {
        if (versions.Count == 0)
        {
            return null;
        }

        if (project.Version == VersionUpdateType.Major)
        {
            return versions.MaxBy(x => x.Version);
        }

        if (project.Version == VersionUpdateType.Minor)
        {
            return versions.Where(x =>
                x.Version.Major == currentVersion.Major && x.Version.Minor > currentVersion.Minor).Max();
        }

        if (project.Version == VersionUpdateType.Patch)
        {
            return versions.Where(x =>
                x.Version.Major == currentVersion.Major &&
                x.Version.Minor == currentVersion.Minor &&
                x.Version.Build > currentVersion.Build).Max();
        }

        throw new NotSupportedException($"Version configuration {project.Version} is not supported");
    }

    private async Task<HashSet<DependencyDetails>> GetLatestVersions(
        ICollection<DependencyDetails> dependenciesToCheck,
        Project project)
    {
        var updates = new HashSet<DependencyDetails>();
        foreach (var dependency in dependenciesToCheck)
        {
            logger.Verbose("Processing {PackageName}:{PackageVersion}", dependency.Name, dependency.Version);
            var versions = await GetVersions(dependency, project);
            var latestVersion = GetMaxVersion(versions, dependency.Version, project);
            if (latestVersion is null)
            {
                logger.Warning("{PacakgeName} unable to find in sources", dependency.Name);
                continue;
            }

            if (latestVersion.Version == dependency.Version)
            {
                logger.Information("{PackageName} no new version found", dependency.Name);
                continue;
            }

            logger.Information("{PacakgeName} new version {Version} available", dependency.Name, latestVersion);
            updates.Add(dependency with { Version = latestVersion.Version });
        }

        return updates;
    }

    private async Task<IReadOnlyCollection<DependencyDetails>> GetVersions(
        DependencyDetails dependency,
        Project project)
    {
        if (memoryCache.TryGetValue<IReadOnlyCollection<DependencyDetails>>(dependency.Name, out var versions) &&
            versions is not null)
        {
            cacheKeys.Add(dependency.Name);
            return versions;
        }

        var packages = await projectUpdater.GetVersions(dependency, project);
        memoryCache.Set(dependency.Name, packages);
        cacheKeys.Add(dependency.Name);
        return packages;
    }
}
