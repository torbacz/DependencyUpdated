using DependencyUpdated.Projects.Npm.Models;
using Refit;

namespace DependencyUpdated.Projects.Npm;

public interface INpmApi
{
    [Get("/{packageName}")]
    Task<NpmPackageInfo> GetPackageData(string packageName);
}
