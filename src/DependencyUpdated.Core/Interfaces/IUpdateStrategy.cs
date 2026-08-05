using DependencyUpdated.Core.Config;

namespace DependencyUpdated.Core.Interfaces;

public interface IUpdateStrategy
{
    Task Update(Project project, string repositoryPath);
}
