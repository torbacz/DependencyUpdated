using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.IO.Enumeration;

namespace DependencyUpdated.Core;

public sealed class Updater(IServiceProvider serviceProvider, IOptions<UpdaterConfig> config)
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
                if (!Path.Exists(directory))
                {
                    throw new FileNotFoundException("Search path not found", directory);
                }

                var projectFiles = updater.GetAllProjectFiles(directory);
                var allDependenciesToUpdate =
                    await updater.ExtractAllPackagesThatNeedToBeUpdated(projectFiles, configEntry);

                if (allDependenciesToUpdate.Count == 0)
                {
                    continue;
                }

                var uniqueListOfDependencies = allDependenciesToUpdate.DistinctBy(x => x.Name).ToList();
                var projectName = ResolveProjectName(configEntry, directory);
                foreach (var group in configEntry.Groups)
                {
                    var matchesForGroup = uniqueListOfDependencies
                        .Where(x => FileSystemName.MatchesSimpleExpression(group, x.Name)).ToArray();
                    if (matchesForGroup.Length == 0)
                    {
                        continue;
                    }

                    uniqueListOfDependencies.RemoveAll(x => FileSystemName.MatchesSimpleExpression(group, x.Name));
                    repositoryProvider.SwitchToUpdateBranch(repositoryPath, projectName, group);

                    var allUpdates = updater.HandleProjectUpdate(projectFiles, matchesForGroup);
                    if (allUpdates.Count == 0)
                    {
                        continue;
                    }

                    repositoryProvider.CommitChanges(repositoryPath, projectName, group);
                    await repositoryProvider.SubmitPullRequest(allUpdates.DistinctBy(x => x.PackageName).ToArray(),
                        projectName, group);
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
}