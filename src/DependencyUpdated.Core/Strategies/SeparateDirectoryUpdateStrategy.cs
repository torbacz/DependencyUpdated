using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;

namespace DependencyUpdated.Core.Strategies;

public sealed class SeparateDirectoryUpdateStrategy(
    IProjectUpdater projectUpdater,
    IRepositoryProvider repositoryProvider,
    DependencyUpdateProcessor processor)
    : IUpdateStrategy
{
    public async Task Update(Project project, string repositoryPath)
    {
        foreach (var directory in project.Directories)
        {
            var projectFiles = projectUpdater.GetAllProjectFiles(directory);
            var projectName = Path.GetFileName(directory);
            var alreadyProcessed = new List<DependencyDetails>();
            var dependencies = await projectUpdater.ExtractAllPackages(projectFiles);

            foreach (var group in project.Groups)
            {
                repositoryProvider.SwitchToUpdateBranch(repositoryPath, projectName, group);
                var updates = await processor.ProcessDirectoryUpdate(
                    project, projectFiles, dependencies, alreadyProcessed, projectName, group, repositoryPath);
                if (updates is null)
                {
                    repositoryProvider.CleanAndSwitchToDefaultBranch(repositoryPath);
                    continue;
                }

                repositoryProvider.PushChanges(repositoryPath, projectName, group);
                await repositoryProvider.SubmitPullRequest(updates, projectName, group);
                repositoryProvider.CleanAndSwitchToDefaultBranch(repositoryPath);
            }
        }
    }
}
