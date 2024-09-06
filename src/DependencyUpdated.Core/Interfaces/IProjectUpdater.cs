using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Models;

namespace DependencyUpdated.Core.Interfaces;

public interface IProjectUpdater
{
    Task<ICollection<DependencyDetails>> ExtractAllPackagesThatNeedToBeUpdated(IReadOnlyCollection<string> fullPath,
        Project projectConfiguration);

    IReadOnlyCollection<string> GetAllProjectFiles(string searchPath);

    IReadOnlyCollection<UpdateResult> HandleProjectUpdate(IReadOnlyCollection<string> fullPath,
        ICollection<DependencyDetails> dependenciesToUpdate);
}