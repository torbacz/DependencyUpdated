namespace DependencyUpdated.Projects.Npm.Models;

public record NpmPackageInfo(Dictionary<string, NpmPackageVersion> Versions);