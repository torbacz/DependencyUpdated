using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace DependencyUpdated.Repositories.AzureDevOps;

public static class ConfigureServices
{
    public static IServiceCollection RegisterAzureDevOps(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddKeyedSingleton<IRepositoryProvider, AzureDevOps>(RepositoryType.AzureDevOps);

        return serviceCollection;
    }
}