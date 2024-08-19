namespace DependencyUpdated.Core;

public interface IRepositoryProvider
{
    public void SwitchToDefaultBranch(string repositoryPath);

    public void SwitchToUpdateBranch(string repositoryPath, string projectName);

    public void CommitChanges(string repositoryPath, string projectName);

    public Task SubmitPullRequest(IReadOnlyCollection<UpdateResult> updates, string projectName);
}