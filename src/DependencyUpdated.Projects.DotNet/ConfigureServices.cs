using DependencyUpdated.Core;
using Microsoft.Extensions.DependencyInjection;

namespace DependencyUpdated.Projects.DotNet;

public static class ConfigureServices
{
    public static IServiceCollection RegisterDotNetServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddKeyedSingleton<IProjectUpdater, DotNetUpdater>(ProjectType.DotNet);

        return serviceCollection;
    }
}