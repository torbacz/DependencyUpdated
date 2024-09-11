using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Repositories.AzureDevOps.Dto;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using Microsoft.Extensions.Options;
using Serilog;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DependencyUpdated.Repositories.AzureDevOps;

[ExcludeFromCodeCoverage]
internal sealed class AzureDevOps(TimeProvider timeProvider, IOptions<UpdaterConfig> config, ILogger logger)
    : IRepositoryProvider
{
    private const string GitCommitMessage = "Bump dependencies";
    private const string RemoteName = "origin";

    public void SwitchToDefaultBranch(string repositoryPath)
    {
        var branchName = config.Value.AzureDevOps.TargetBranchName;
        logger.Information("Switching {Repository} to branch {Branch}", repositoryPath, branchName);
        using var repo = new Repository(repositoryPath);
        var branch = GetGitBranch(repo, branchName) 
                     ?? throw new InvalidOperationException($"Branch {branchName} doesn't exist");

        Commands.Checkout(repo, branch);
    }

    public void SwitchToUpdateBranch(string repositoryPath, string projectName, string group)
    {
        var gitBranchName = CreateGitBranchName(projectName, config.Value.AzureDevOps.BranchName, group);
        logger.Information("Switching {Repository} to branch {Branch}", repositoryPath, gitBranchName);
        using var repo = new Repository(repositoryPath);
        var branch = GetGitBranch(repo, gitBranchName);
        if (branch == null)
        {
            logger.Information("Branch {Branch} does not exists. Creating", gitBranchName);
            branch = repo.CreateBranch(gitBranchName);
            branch = repo.Branches.Update(branch, updater =>
            {
                updater.UpstreamBranch = branch.CanonicalName;
                updater.Remote = RemoteName;
            });
        }

        Commands.Checkout(repo, branch);
    }

    public void CommitChanges(string repositoryPath, string projectName, string group)
    {
        var gitBranchName = CreateGitBranchName(projectName, config.Value.AzureDevOps.BranchName, group);
        logger.Information("Commiting {Repository} to branch {Branch}", repositoryPath, gitBranchName);
        using var repo = new Repository(repositoryPath);
        
        var status = repo.RetrieveStatus();
        if (!status.IsDirty)
        {
            logger.Information("No changes to commit");
            return;
        }
        
        Commands.Stage(repo, "*");
        var author = new Signature(config.Value.AzureDevOps.Username, config.Value.AzureDevOps.Email,
            timeProvider.GetUtcNow());
        repo.Commit(GitCommitMessage, author, author);
        var branch = GetGitBranch(repo, gitBranchName);
        var options = new PushOptions { CredentialsProvider = CreateGitCredentialsProvider() };
        repo.Network.Push(branch, options);
    }

    public async Task SubmitPullRequest(IReadOnlyCollection<UpdateResult> updates, string projectName, string group)
    {
        using var client = new HttpClient();
        var configValue = config.Value.AzureDevOps;
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($":{configValue.PAT}")));
        
        var gitBranchName = CreateGitBranchName(projectName, config.Value.AzureDevOps.BranchName, group);
        var sourceBranch = $"refs/heads/{gitBranchName}";
        var targetBranch = $"refs/heads/{configValue.TargetBranchName}";
        var baseUrl =
            $"https://dev.azure.com/{configValue.Organization}/{configValue.Project}/_apis/git/repositories/{configValue.Repository}/pullrequests?api-version=6.0";

        if (await CheckIfPrExists(client, baseUrl, sourceBranch, targetBranch))
        {
            logger.Information("PR from {SourceBranch} to {TargetBranch} already exists. Skipping creating PR", gitBranchName, configValue.TargetBranchName);
            return;
        }
        
        var prTitile = $"[AutoUpdate] Update dependencies - {projectName}";
        var prDescription = CreatePrDescription(updates);

        logger.Information("Creating new PR");
        var pr = new PullRequest(sourceBranch, targetBranch, prTitile, prDescription);

        var jsonString = JsonSerializer.Serialize(pr);
        var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

        var result = await client.PostAsync(baseUrl, content);
        result.EnsureSuccessStatusCode();

        if (result.StatusCode == HttpStatusCode.NonAuthoritativeInformation)
        {
            throw new InvalidOperationException("Invalid PAT token provided");
        }

        var response = await result.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, };
        var responseObject = JsonSerializer.Deserialize<PullRequestResponse>(response, options) ??
                             throw new InvalidOperationException("Missing response from API");

        logger.Information("New PR created {Id}", responseObject.PullRequestId);
        if (configValue.AutoComplete)
        {
            logger.Information("Setting autocomplete for PR {Id}", responseObject.PullRequestId);
            baseUrl =
                $"https://dev.azure.com/{configValue.Organization}/{configValue.Project}/_apis/git/repositories/{configValue.Repository}/pullrequests/{responseObject.PullRequestId}?api-version=6.0";
            var autoComplete = new PullRequestUpdate(responseObject.CreatedBy,
                new GitPullRequestCompletionOptions(true, false, GitPullRequestMergeStrategy.Squash));
            jsonString = JsonSerializer.Serialize(autoComplete);
            content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            result = await client.PatchAsync(baseUrl, content);
            result.EnsureSuccessStatusCode();
        }

        if (configValue.WorkItemId.HasValue)
        {
            logger.Information("Setting work item {ConfigValueWorkItemId} relation to {PullRequestId}",
                configValue.WorkItemId.Value, responseObject.PullRequestId);
            var workItemUpdateUrl = new Uri(
                $"https://dev.azure.com/{configValue.Organization}/{configValue.Project}/_apis/wit/workitems/{configValue.WorkItemId.Value}?api-version=6.0");
            var patchValue = new[]
            {
                new
                {
                    op = "add",
                    path = "/relations/-",
                    value = new
                    {
                        rel = "ArtifactLink",
                        url = responseObject.ArtifactId,
                        attributes = new { name = "Pull Request" }
                    }
                }
            };

            jsonString = JsonSerializer.Serialize(patchValue);
            content = new StringContent(jsonString, Encoding.UTF8, "application/json-patch+json");
            result = await client.PatchAsync(workItemUpdateUrl, content);
            result.EnsureSuccessStatusCode();
        }
    }

    private static string CreateGitBranchName(string projectName, string branchName, string group)
    {
        var newBranchName = $"{branchName.ToLower()}/{projectName.ToLower()}/{group.ToLower()}";
        newBranchName = newBranchName.Replace(".", "/").Replace("*", "asterix");
        return newBranchName;
    }
    
    private async Task<bool> CheckIfPrExists(HttpClient client, string baseUrl, string sourceBranchName,
        string targetBranchName)
    {
        var response = await client.GetAsync(baseUrl);
        response.EnsureSuccessStatusCode();

        var jsonString = await response.Content.ReadAsStringAsync();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, };
        var responseObject = JsonSerializer.Deserialize<PullRequestArray>(jsonString, options) ??
                             throw new InvalidOperationException("Missing response from API");

        return responseObject.Value.Any(pr => pr.SourceRefName == sourceBranchName && pr.TargetRefName == targetBranchName);
    }

    private Branch? GetGitBranch(Repository repo, string branchName)
    {
        var options = new FetchOptions { CredentialsProvider = CreateGitCredentialsProvider() };
        Commands.Fetch(repo, RemoteName, ArraySegment<string>.Empty, options, string.Empty);
        
        var localBranch = repo.Branches[branchName];
        if (localBranch is not null)
        {
            return localBranch;
        }
        
        var remoteBranch = repo.Branches[$"{RemoteName}/{branchName}"];
        if (remoteBranch is null)
        {
            return null;
        }
        
        // Create local tracking branch
        var createdBranch = repo.CreateBranch(branchName, remoteBranch.Tip);
        repo.Branches.Update(createdBranch, b => b.TrackedBranch = remoteBranch.CanonicalName);
        return createdBranch;
    }

    private string CreatePrDescription(IReadOnlyCollection<UpdateResult> updates)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("DependencyUpdater auto update");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("Log:");
        foreach (var update in updates)
        {
            stringBuilder.AppendLine($"Bump {update.PackageName}: {update.OldVersion} -> {update.NewVersion}");
        }

        return stringBuilder.ToString();
    }

    private CredentialsHandler? CreateGitCredentialsProvider()
    {
        if (string.IsNullOrEmpty(config.Value.AzureDevOps.Username))
        {
            return null;
        }

        if (string.IsNullOrEmpty(config.Value.AzureDevOps.PAT))
        {
            return null;
        }

        return (_, _, _) => new UsernamePasswordCredentials()
        {
            Username = config.Value.AzureDevOps.Username, Password = config.Value.AzureDevOps.PAT
        };
    }
}