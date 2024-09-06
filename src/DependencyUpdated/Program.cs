using CommandLine;
using DependencyUpdated.Core;
using DependencyUpdated.Core.Config;
using DependencyUpdated.Projects.DotNet;
using DependencyUpdated.Repositories.AzureDevOps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using System.ComponentModel.DataAnnotations;
using System.IO.Enumeration;
using ILogger = Serilog.ILogger;

namespace DependencyUpdated;

public static class Program
{
    private static IConfiguration _configuration = default!;
    private static IServiceProvider _serviceProvider = default!;

    public static async Task Main(string[] args)
    {
       await Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(RunApplication);
    }

    private static async Task RunApplication(Options options)
    {
        Configure(options);
        ConfigureServices();

        var config = _serviceProvider.GetRequiredService<IOptions<UpdaterConfig>>();
        config.Value.ApplyDefaultValues();
        var validationResult = config.Value.Validate(new ValidationContext(config.Value)).ToList();
        if (validationResult.Count != 0)
        {
            throw new OptionsValidationException(nameof(UpdaterConfig), typeof(UpdaterConfig),
                validationResult.Where(x => !string.IsNullOrEmpty(x.ErrorMessage)).Select(x => x.ErrorMessage!));
        }
        
        var repositoryProvider =
            _serviceProvider.GetRequiredKeyedService<IRepositoryProvider>(config.Value.RepositoryType);
        var repositoryPath = Environment.CurrentDirectory;
        repositoryProvider.SwitchToDefaultBranch(repositoryPath);

        foreach (var configEntry in config.Value.Projects)
        {
            var updater = _serviceProvider.GetRequiredKeyedService<IProjectUpdater>(configEntry.Type);

            foreach (var directory in configEntry.Directories)
            {
                if (!Path.Exists(directory))
                {
                    throw new FileNotFoundException("Search path not found", directory);
                }

                var projectFiles = updater.GetAllProjectFiles(directory);
                var allDependenciesToUpdate =
                    await updater.ExtractAllPackagesThatNeedToBeUpdated(projectFiles, configEntry);

                if (allDependenciesToUpdate.Count == 0)
                {
                    continue;
                }

                var uniqueListOfDependencies = allDependenciesToUpdate.DistinctBy(x => x.Name).ToList();
                var projectName = ResolveProjectName(configEntry, directory);
                foreach (var group in configEntry.Groups)
                {
                    var matchesForGroup = uniqueListOfDependencies
                        .Where(x => FileSystemName.MatchesSimpleExpression(group, x.Name)).ToArray();
                    if (matchesForGroup.Length == 0)
                    {
                        continue;
                    }
                    
                    uniqueListOfDependencies.RemoveAll(x => FileSystemName.MatchesSimpleExpression(group, x.Name));
                    repositoryProvider.SwitchToUpdateBranch(repositoryPath, projectName, group);

                    var allUpdates = updater.HandleProjectUpdate(projectFiles, matchesForGroup);
                    if (allUpdates.Count == 0)
                    {
                        continue;
                    }

                    repositoryProvider.CommitChanges(repositoryPath, projectName, group);
                    await repositoryProvider.SubmitPullRequest(allUpdates.DistinctBy(x => x.PackageName).ToArray(), projectName, group);
                    repositoryProvider.SwitchToDefaultBranch(repositoryPath);
                }
            }
        }
    }

    private static string ResolveProjectName(Project project, string directory)
    {
        if (!project.EachDirectoryAsSeparate)
        {
            return project.Name;
        }

        return Path.GetFileName(directory);
    }

    private static void Configure(Options appOptions)
    {
        var configPath = string.IsNullOrEmpty(appOptions.ConfigPath) ? "config.json" : appOptions.ConfigPath;
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(configPath, false)
            .AddEnvironmentVariables()
            .Build();
    }

    private static void ConfigureServices()
    {
        var services = new ServiceCollection();
        var logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
        services
            .AddSingleton<ILogger>(logger)
            .AddSingleton(TimeProvider.System)
            .AddMemoryCache()
            .RegisterDotNetServices()
            .RegisterGithub()
            .AddOptions<UpdaterConfig>().Bind(_configuration.GetSection("UpdaterConfig"));

        _serviceProvider = services.BuildServiceProvider();
    }
}