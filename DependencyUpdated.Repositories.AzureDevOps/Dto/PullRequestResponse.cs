namespace DependencyUpdated.Repositories.AzureDevOps.Dto;

public record PullRequestResponse(int PullRequestId, User CreatedBy, string Url, string ArtifactId);