namespace DependencyUpdated.Core;

public interface IProjectUpdater
{
    public Task<IReadOnlyCollection<UpdateResult>> UpdateProject(string searchPath);
}