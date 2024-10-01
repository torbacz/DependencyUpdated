using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;
using System.Net.Http.Headers;
using System.Text.Json;

namespace DependencyUpdated.Repositories.AzureDevOps;

public static class ConfigureServices
{
    public static IServiceCollection RegisterAzureDevOps(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddKeyedSingleton<IRepositoryProvider, AzureDevOps>(RepositoryType.AzureDevOps);
        serviceCollection.AddSingleton<AzureApiHeaderHandler>();
        serviceCollection.AddRefitClient<IAzureDevOpsClient>(_ => new RefitSettings()
        {
            HttpMessageHandlerFactory = () => new LoggingHandler(),
            ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            })
        }).ConfigureHttpClient((serviceProvider, x) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<UpdaterConfig>>().Value;
            x.BaseAddress =
                new Uri($"https://dev.azure.com/{options.AzureDevOps.Organization}/{options.AzureDevOps.Project}");
            x.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }).AddHttpMessageHandler<AzureApiHeaderHandler>();
        return serviceCollection;
    }
}