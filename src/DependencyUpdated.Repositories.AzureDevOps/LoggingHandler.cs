using Refit;
using System.Diagnostics;
using System.Net;

namespace DependencyUpdated.Repositories.AzureDevOps;

internal sealed class LoggingHandler : HttpClientHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Debug.WriteLine("Request:");
        Debug.WriteLine(request.ToString());
        if (request.Content != null)
        {
            Debug.WriteLine(await request.Content.ReadAsStringAsync(cancellationToken));
        }

        Debug.WriteLine(string.Empty);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NonAuthoritativeInformation)
        {
            throw await ApiException.Create("Invalid PAT token", request, request.Method, response, new RefitSettings());
        }

        Debug.WriteLine("Response:");
        Debug.WriteLine(response.ToString());
        Debug.WriteLine(await response.Content.ReadAsStringAsync(cancellationToken));

        return response;
    }
}