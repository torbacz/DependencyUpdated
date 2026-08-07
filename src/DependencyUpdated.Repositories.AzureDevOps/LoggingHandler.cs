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
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        Debug.WriteLine(responseBody);
        if (!response.IsSuccessStatusCode)
        {
            await Console.Error.WriteLineAsync(
                $"Azure DevOps request {request.Method} {request.RequestUri} failed with " +
                $"{(int)response.StatusCode}: {responseBody}");
        }

        return response;
    }
}
