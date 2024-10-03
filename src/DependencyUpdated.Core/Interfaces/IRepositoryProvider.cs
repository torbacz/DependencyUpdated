using DependencyUpdated.Core.Models;

namespace DependencyUpdated.Core.Interfaces;

public interface IRepositoryProvider
{
    public void CleanAndSwitchToDefaultBranch(string repositoryPath);

    public void SwitchToUpdateBranch(string repositoryPath, string projectName, string group);

    public void CommitChanges(string repositoryPath, string projectName, string group);

    public Task SubmitPullRequest(IReadOnlyCollection<UpdateResult> updates, string projectName, string group);
}