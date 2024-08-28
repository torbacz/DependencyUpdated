using DependencyUpdated.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DependencyUpdated.Repositories.AzureDevOps;

public static class ConfigureServices
{
    public static IServiceCollection RegisterGithub(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddKeyedSingleton<IRepositoryProvider, AzureDevOps>(RepositoryType.AzureDevOps);

        return serviceCollection;
    }
}