namespace DependencyUpdated.Repositories.AzureDevOps.Dto;

public sealed record WorkItemUpdatePatch(string Op, string Path, WorkItemUpdateRelation Value);