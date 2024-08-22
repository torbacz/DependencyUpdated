using DependencyUpdated.Core.Config;

namespace DependencyUpdated.Core;

public interface IProjectUpdater
{ 
    Task<ICollection<DependencyDetails>> ExtractAllPackagesThatNeedToBeUpdated(string fullPath, Project projectConfiguration);
    IEnumerable<string> GetAllProjectFiles(string searchPath);
    IReadOnlyCollection<UpdateResult> HandleProjectUpdate(string fullPath, ICollection<DependencyDetails> dependenciesToUpdate);
}