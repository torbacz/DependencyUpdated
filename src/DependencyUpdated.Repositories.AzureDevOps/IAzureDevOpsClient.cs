using DependencyUpdated.Repositories.AzureDevOps.Dto;
using Refit;

namespace DependencyUpdated.Repositories.AzureDevOps;

public interface IAzureDevOpsClient
{
    const string ReviewersResource = "reviewers";
    
    [Get("/_apis/git/repositories/{repository}/pullrequests?api-version=6.0")]
    Task<PullRequestArray> GetPullRequests(string repository);

    [Post("/_apis/git/repositories/{repository}/pullrequests?api-version=6.0")]
    Task<PullRequestResponse> CreatePullRequest(string repository, [Body] PullRequest pullRequestInfo);

    [Patch("/_apis/git/repositories/{repository}/pullrequests/{pullRequestId}?api-version=6.0")]
    Task<HttpResponseMessage> UpdatePullRequest(string repository, int pullRequestId, [Body] PullRequestUpdate pullRequestUpdate);

    [Patch("/_apis/wit/workitems/{workItemId}?api-version=6.0")]
    [Headers("Content-Type: application/json-patch+json")]
    Task<HttpResponseMessage> UpdateWorkItemRelation(int workItemId, [Body] WorkItemUpdatePatch[] patchValue);

    [Put("/_apis/git/repositories/{repository}/pullRequests/{pullRequestId}/" + ReviewersResource + "/{reviewerId}?api-version=6.0")]
    Task<HttpResponseMessage> Approve(string repository, int pullRequestId, string reviewerId, [Body] ApproveBody body);
}