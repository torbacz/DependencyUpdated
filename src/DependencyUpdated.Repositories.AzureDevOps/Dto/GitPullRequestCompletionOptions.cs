namespace DependencyUpdated.Repositories.AzureDevOps.Dto;

public record GitPullRequestCompletionOptions(bool DeleteSourceBranch, bool BypassPolicy, GitPullRequestMergeStrategy MergeStrategy);