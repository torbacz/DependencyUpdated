namespace DependencyUpdated.Projects.Npm.Models;

public record PackageRoot(Dictionary<string, string>? Dependencies, Dictionary<string, string>? DevDependencies);