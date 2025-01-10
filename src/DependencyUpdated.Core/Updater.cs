using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Core.Models.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using System.IO.Enumeration;

namespace DependencyUpdated.Core;

public sealed class Updater(IServiceProvider serviceProvider, IOptions<UpdaterConfig> config, ILogger logger, IMemoryCache memoryCache)
{
    private readonly List<string> _cacheKeys = new();
    
    public async Task DoUpdate()
    {
        var repositoryProvider =
            serviceProvider.GetRequiredKeyedService<IRepositoryProvider>(config.Value.RepositoryType);
        var repositoryPath = Environment.CurrentDirectory;
        repositoryProvider.CleanAndSwitchToDefaultBranch(repositoryPath);

        foreach (var project in config.Value.Projects)
        {
            var updater = serviceProvider.GetRequiredKeyedService<IProjectUpdater>(project.Type);
            foreach (var directory in project.Directories)
            {
                var projectFiles = updater.GetAllProjectFiles(directory);
                var projectName = ResolveProjectName(project, directory);
                var alreadyProcessed = new List<DependencyDetails>();
                var allProjectDependencies = await updater.ExtractAllPackages(projectFiles);
                logger.Debug("Found packages {Packages} in projects {Project}", allProjectDependencies, projectFiles);
                foreach (var group in project.Groups)
                {
                    repositoryProvider.SwitchToUpdateBranch(repositoryPath, projectName, group);
                    var filteredPackages = FilterPackages(allProjectDependencies, alreadyProcessed, group, project);
                    if (filteredPackages.Count == 0)
                    {
                        continue;
                    }

                    logger.Debug("Filtered packages {Packages}", filteredPackages);
                    alreadyProcessed.AddRange(filteredPackages);
                    var allDependenciesToUpdate = await GetLatestVersions(filteredPackages, updater, project);
                    if (allDependenciesToUpdate.Count == 0)
                    {
                        continue;
                    }

                    logger.Verbose("Found new versions: {Packages}", allDependenciesToUpdate);
                    var allUpdates = updater.HandleProjectUpdate(project, projectFiles, allDependenciesToUpdate);
                    if (allUpdates.Count == 0)
                    {
                        continue;
                    }

                    logger.Information("Updated packages {Packages}", allUpdates);
                    repositoryProvider.CommitChanges(repositoryPath, projectName, group);
                    await repositoryProvider.SubmitPullRequest(allUpdates, projectName, group);
                    repositoryProvider.CleanAndSwitchToDefaultBranch(repositoryPath);
                }
            }

            foreach (var key in _cacheKeys)
            {
                memoryCache.Remove(key);
            }
            
            _cacheKeys.Clear();
        }
    }
    
    internal void AddToCache(DependencyDetails dependencyDetails, IReadOnlyCollection<DependencyDetails> packages)
    {
        memoryCache.Set(dependencyDetails.Name, packages);
        _cacheKeys.Add(dependencyDetails.Name);
    }

    private static ICollection<DependencyDetails> FilterPackages(
        ICollection<DependencyDetails> allPackagesFromProjects, IReadOnlyCollection<DependencyDetails> alreadyProcessed,
        string group, Project project)
    {
        if (allPackagesFromProjects.Count == 0)
        {
            return ArraySegment<DependencyDetails>.Empty;
        }

        var basePackages = allPackagesFromProjects.ExceptBy(alreadyProcessed.Select(x => x.Name), x => x.Name);
        if (project.Include.Count > 0)
        {
            basePackages = basePackages.Where(x => project.Include.Any(include => FileSystemName.MatchesSimpleExpression(include, x.Name)));
        }
        
        if (project.Exclude.Count > 0)
        {
            basePackages = basePackages.Where(x => !project.Exclude.Any(exclude => FileSystemName.MatchesSimpleExpression(exclude, x.Name)));
        }

        basePackages = basePackages.Where(x => FileSystemName.MatchesSimpleExpression(group, x.Name)).ToArray();
        return basePackages.ToArray();
    }

    private static string ResolveProjectName(Project project, string directory)
    {
        if (!project.EachDirectoryAsSeparate)
        {
            return project.Name;
        }

        return Path.GetFileName(directory);
    }

    private static DependencyDetails? GetMaxVersion(IReadOnlyCollection<DependencyDetails> versions,
        Version currentVersion,
        Project projectConfiguration)
    {
        if (versions.Count == 0)
        {
            return null;
        }

        if (projectConfiguration.Version == VersionUpdateType.Major)
        {
            return versions.MaxBy(x => x.Version);
        }

        if (projectConfiguration.Version == VersionUpdateType.Minor)
        {
            return versions.Where(x =>
                x.Version.Major == currentVersion.Major && x.Version.Minor > currentVersion.Minor).Max();
        }

        if (projectConfiguration.Version == VersionUpdateType.Patch)
        {
            return versions.Where(x =>
                x.Version.Major == currentVersion.Major && x.Version.Minor == currentVersion.Minor &&
                x.Version.Build > currentVersion.Build).Max();
        }

        throw new NotSupportedException($"Version configuration {projectConfiguration.Version} is not supported");
    }
    
    private async Task<HashSet<DependencyDetails>> GetLatestVersions(
        ICollection<DependencyDetails> allDependenciesToCheck,
        IProjectUpdater projectUpdater, Project projectConfiguration)
    {
        var returnList = new HashSet<DependencyDetails>();
        foreach (var dependencyDetails in allDependenciesToCheck)
        {
            logger.Verbose("Processing {PackageName}:{PackageVersion}", dependencyDetails.Name,
                dependencyDetails.Version);
            var allVersions = await GetVersions(projectUpdater, dependencyDetails, projectConfiguration);
            var latestVersion = GetMaxVersion(allVersions, dependencyDetails.Version, projectConfiguration);
            if (latestVersion is null)
            {
                logger.Warning("{PacakgeName} unable to find in sources", dependencyDetails.Name);
                continue;
            }

            if (latestVersion.Version == dependencyDetails.Version)
            {
                logger.Information("{PackageName} no new version found", dependencyDetails.Name);
                continue;
            }

            logger.Information("{PacakgeName} new version {Version} available", dependencyDetails.Name, latestVersion);
            returnList.Add(dependencyDetails with { Version = latestVersion.Version });
        }

        return returnList;
    }

    private async Task<IReadOnlyCollection<DependencyDetails>> GetVersions(IProjectUpdater projectUpdater,
        DependencyDetails dependencyDetails, Project projectConfiguration)
    {
        if (memoryCache.TryGetValue<IReadOnlyCollection<DependencyDetails>>(dependencyDetails.Name,
                out var versions) && versions is not null)
        {
            return versions;
        }

        var packages = await projectUpdater.GetVersions(dependencyDetails, projectConfiguration);
        AddToCache(dependencyDetails, packages);
        return packages;
    }
}