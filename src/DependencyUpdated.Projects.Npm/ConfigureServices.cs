using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace DependencyUpdated.Projects.Npm;

public static class ConfigureServices
{
    public static IServiceCollection RegisterNpmServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddKeyedSingleton<IProjectUpdater, NpmUpdater>(ProjectType.Npm);
        return serviceCollection;
    }
}