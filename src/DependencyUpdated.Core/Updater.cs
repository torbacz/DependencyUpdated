using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using System.IO.Enumeration;

namespace DependencyUpdated.Core;

public sealed class Updater(IServiceProvider serviceProvider, IOptions<UpdaterConfig> config, ILogger logger)
{
    public async Task DoUpdate()
    {
        var repositoryProvider =
            serviceProvider.GetRequiredKeyedService<IRepositoryProvider>(config.Value.RepositoryType);
        var repositoryPath = Environment.CurrentDirectory;
        repositoryProvider.SwitchToDefaultBranch(repositoryPath);

        foreach (var configEntry in config.Value.Projects)
        {
            var updater = serviceProvider.GetRequiredKeyedService<IProjectUpdater>(configEntry.Type);
            foreach (var directory in configEntry.Directories)
            {
                var projectFiles = updater.GetAllProjectFiles(directory);
                var projectName = ResolveProjectName(configEntry, directory);
                var alreadyProcessed = new List<DependencyDetails>();
                foreach (var group in configEntry.Groups)
                {
                    repositoryProvider.SwitchToUpdateBranch(repositoryPath, projectName, group);
                    var allProjectDependencies =
                        (await updater.ExtractAllPackages(projectFiles)).Except(alreadyProcessed).ToArray();
                    if (allProjectDependencies.Length == 0)
                    {
                        continue;
                    }
                    
                    var matchesForGroup = allProjectDependencies
                        .Where(x => FileSystemName.MatchesSimpleExpression(group, x.Name)).ToArray();
                    if (matchesForGroup.Length == 0)
                    {
                        continue;
                    }
                    
                    var allDependenciesToUpdate = await GetLatestVersions(matchesForGroup, updater, configEntry);
                    if (allDependenciesToUpdate.Count == 0)
                    {
                        continue;
                    }
                    
                    var allUpdates = updater.HandleProjectUpdate(projectFiles, allDependenciesToUpdate);
                    if (allUpdates.Count == 0)
                    {
                        continue;
                    }

                    alreadyProcessed.AddRange(allDependenciesToUpdate);
                    repositoryProvider.CommitChanges(repositoryPath, projectName, group);
                    await repositoryProvider.SubmitPullRequest(allUpdates, projectName, group);
                    repositoryProvider.SwitchToDefaultBranch(repositoryPath);
                }
            }
        }
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
            var allVersions = await projectUpdater.GetVersions(dependencyDetails, projectConfiguration);
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
}