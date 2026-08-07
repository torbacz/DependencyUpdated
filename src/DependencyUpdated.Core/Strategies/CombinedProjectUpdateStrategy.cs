using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;

namespace DependencyUpdated.Core.Strategies;

public sealed class CombinedProjectUpdateStrategy(
    IProjectUpdater projectUpdater,
    IRepositoryProvider repositoryProvider,
    DependencyUpdateProcessor processor)
    : IUpdateStrategy
{
    public async Task Update(Project project, string repositoryPath)
    {
        var alreadyProcessedByDirectory = project.Directories.ToDictionary(
            directory => directory,
            _ => (ICollection<DependencyDetails>)new List<DependencyDetails>());

        foreach (var group in project.Groups)
        {
            repositoryProvider.SwitchToUpdateBranch(repositoryPath, project.Name, group);
            var allUpdates = new HashSet<UpdateResult>();
            IReadOnlyCollection<UpdateResult>? firstUpdates = null;
            var hasChanges = false;

            foreach (var directory in project.Directories)
            {
                var projectFiles = projectUpdater.GetAllProjectFiles(directory);
                var dependencies = await projectUpdater.ExtractAllPackages(projectFiles);
                var updates = await processor.ProcessDirectoryUpdate(
                    project, projectFiles, dependencies, alreadyProcessedByDirectory[directory],
                    project.Name, group, repositoryPath);
                if (updates is null)
                {
                    continue;
                }

                firstUpdates ??= updates;
                foreach (var update in updates)
                {
                    allUpdates.Add(update);
                }

                hasChanges = true;
            }

            if (hasChanges)
            {
                repositoryProvider.PushChanges(repositoryPath, project.Name, group);
                var updatesForPullRequest = firstUpdates!.Count == allUpdates.Count
                    ? firstUpdates
                    : allUpdates;
                await repositoryProvider.SubmitPullRequest(updatesForPullRequest, project.Name, group);
            }

            repositoryProvider.CleanAndSwitchToDefaultBranch(repositoryPath);
        }
    }
}
