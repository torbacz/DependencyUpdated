using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Models;

namespace DependencyUpdated.Core.Interfaces;

public interface IProjectUpdater
{
    Task<ICollection<DependencyDetails>> ExtractAllPackages(IReadOnlyCollection<string> fullPath);

    IReadOnlyCollection<string> GetAllProjectFiles(string searchPath);

    IReadOnlyCollection<UpdateResult> HandleProjectUpdate(Project project, IReadOnlyCollection<string> fullPath,
        ICollection<DependencyDetails> dependenciesToUpdate);

    Task<IReadOnlyCollection<DependencyDetails>> GetVersions(DependencyDetails package,
        Project projectConfiguration);
}