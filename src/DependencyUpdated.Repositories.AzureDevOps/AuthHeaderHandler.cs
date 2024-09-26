using DependencyUpdated.Core.Config;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;

namespace DependencyUpdated.Repositories.AzureDevOps;

internal sealed class AuthHeaderHandler(IOptions<UpdaterConfig> config) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var approverPat = config.Value.AzureDevOps.ApproverPAT;
        if (request.RequestUri?.AbsolutePath.Contains(IAzureDevOpsClient.ReviewersResource) == true && 
            !string.IsNullOrEmpty(approverPat))
        {
            request.Headers.Authorization ??= CreateToken(approverPat);
        }

        request.Headers.Authorization ??= CreateToken(config.Value.AzureDevOps.PAT!);
        return base.SendAsync(request, cancellationToken);
    }

    private static AuthenticationHeaderValue CreateToken(string token)
    {
        return new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($":{token}")));
    }
}