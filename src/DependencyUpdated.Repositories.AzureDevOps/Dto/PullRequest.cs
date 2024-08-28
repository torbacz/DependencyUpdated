namespace DependencyUpdated.Repositories.AzureDevOps.Dto;

public record PullRequest(string SourceRefName, string TargetRefName, string Title, string Description);