using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Strategies;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace DependencyUpdated.Core;

public sealed class Updater(
    IServiceProvider serviceProvider,
    IOptions<UpdaterConfig> config,
    ILogger logger,
    IMemoryCache memoryCache)
{
    public async Task DoUpdate()
    {
        var repositoryProvider =
            serviceProvider.GetRequiredKeyedService<IRepositoryProvider>(config.Value.RepositoryType);
        var repositoryPath = Environment.CurrentDirectory;
        repositoryProvider.CleanAndSwitchToDefaultBranch(repositoryPath);

        foreach (var project in config.Value.Projects)
        {
            var projectUpdater = serviceProvider.GetRequiredKeyedService<IProjectUpdater>(project.Type);
            var processor = new DependencyUpdateProcessor(
                projectUpdater, repositoryProvider, logger, memoryCache);
            IUpdateStrategy strategy = project.EachDirectoryAsSeparate
                ? new SeparateDirectoryUpdateStrategy(projectUpdater, repositoryProvider, processor)
                : new CombinedProjectUpdateStrategy(projectUpdater, repositoryProvider, processor);

            await strategy.Update(project, repositoryPath);
            processor.ClearCache();
        }
    }
}
