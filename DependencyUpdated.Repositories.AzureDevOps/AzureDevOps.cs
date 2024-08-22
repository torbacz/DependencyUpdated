using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DependencyUpdated.Core;
using DependencyUpdated.Core.Config;
using DependencyUpdated.Repositories.AzureDevOps.Dto;
using LibGit2Sharp;
using Microsoft.Extensions.Options;
using Serilog;

namespace DependencyUpdated.Repositories.AzureDevOps;

internal sealed class AzureDevOps(TimeProvider timeProvider, IOptions<UpdaterConfig> config, ILogger logger) : IRepositoryProvider
{
    private const string GitCommitMessage = "Bump dependencies";
    
    public void SwitchToDefaultBranch(string repositoryPath)
    {
        var branchName = config.Value.AzureDevOps.TargetBranchName;
        logger.Information("Switching {Repository} to branch {Branch}", repositoryPath, branchName);
        using (var repo = new Repository(repositoryPath)) 
        {
            var branch = repo.Branches[branchName];
            if (branch == null)
            {
                throw new Exception($"Branch {branchName} doesn't exists");
            }

            Commands.Checkout(repo, branch);
        }
    }

    public void SwitchToUpdateBranch(string repositoryPath, string projectName, string group)
    {
        var gitBranchName = CreateGitBranchName(projectName, config.Value.AzureDevOps.BranchName, group);
        logger.Information("Switching {Repository} to branch {Branch}", repositoryPath, gitBranchName);
        using (var repo = new Repository(repositoryPath)) 
        {
            var branch = repo.Branches[gitBranchName];
            if (branch == null)
            {
                logger.Information("Branch {Branch} does not exists. Creating", gitBranchName);
                branch = repo.CreateBranch(gitBranchName);
                branch = repo.Branches.Update(branch, updater =>
                {
                    updater.UpstreamBranch = branch.CanonicalName;
                    updater.Remote = "origin";
                });
            }

            Commands.Checkout(repo, branch);
        }
    }

    
    public void CommitChanges(string repositoryPath, string projectName, string group)
    {
        var gitBranchName = CreateGitBranchName(projectName, config.Value.AzureDevOps.BranchName, group);
        logger.Information("Commiting {Repository} to branch {Branch}", repositoryPath, gitBranchName);
        using (var repo = new Repository(repositoryPath))
        {
            Commands.Stage(repo, "*");
            var author = new Signature(config.Value.AzureDevOps.Username, config.Value.AzureDevOps.Email,
                timeProvider.GetUtcNow());
            repo.Commit(GitCommitMessage, author, author);
            var options = new PushOptions();

            if (!string.IsNullOrEmpty(config.Value.AzureDevOps.Username) &&
                !string.IsNullOrEmpty(config.Value.AzureDevOps.PAT))
            {
                options.CredentialsProvider = (_, _, _) =>
                    new UsernamePasswordCredentials()
                    {
                        Username = config.Value.AzureDevOps.Username,
                        Password = config.Value.AzureDevOps.PAT
                    };
            }

            repo.Network.Push(repo.Branches[gitBranchName], options);
        }
    }

    public async Task SubmitPullRequest(IReadOnlyCollection<UpdateResult> updates, string projectName, string group)
    {
        var prTitile = $"[AutoUpdate] Update dependencies - {projectName}";
        var prDescription = CreatePrDescription(updates);
        var gitBranchName = CreateGitBranchName(projectName, config.Value.AzureDevOps.BranchName, group);
        var configValue = config.Value.AzureDevOps;
        var sourceBranch = $"refs/heads/{gitBranchName}";
        var targetBranch = $"refs/heads/{configValue.TargetBranchName}";
        var baseUrl =
            $"https://dev.azure.com/{configValue.Organization}/{configValue.Project}/_apis/git/repositories/{configValue.Repository}/pullrequests?api-version=6.0";

        logger.Information("Creating new PR");
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($":{configValue.PAT}")));
            var pr = new PullRequest(sourceBranch, targetBranch, prTitile, prDescription);
            
            var jsonString = JsonSerializer.Serialize(pr);
            var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
            
            var result = await client.PostAsync(baseUrl, content);
            result.EnsureSuccessStatusCode();
            
            if (result.StatusCode == HttpStatusCode.NonAuthoritativeInformation)
            {
                throw new Exception("Invalid PAT token provided");
            }
            
            var response = await result.Content.ReadAsStringAsync(); 
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            var responseObject = JsonSerializer.Deserialize<PullRequestResponse>(response, options);
            if (responseObject is null)
            {
                throw new Exception("Missing response from API");
            }
            
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
        }
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

    private static string CreateGitBranchName(string projectName, string branchName, string group)
    {
        return $"{projectName.ToLower()}/{branchName.ToLower()}/{group.ToLower().Replace("*","-asterix-")}";
    }
}