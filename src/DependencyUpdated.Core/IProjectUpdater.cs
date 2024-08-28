using DependencyUpdated.Core.Config;

namespace DependencyUpdated.Core;

public interface IProjectUpdater
{
    Task<ICollection<DependencyDetails>> ExtractAllPackagesThatNeedToBeUpdated(string fullPath,
        Project projectConfiguration);

    IReadOnlyCollection<string> GetAllProjectFiles(string searchPath);

    IReadOnlyCollection<UpdateResult> HandleProjectUpdate(string fullPath,
        ICollection<DependencyDetails> dependenciesToUpdate);
}