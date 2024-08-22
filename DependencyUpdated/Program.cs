using System.ComponentModel.DataAnnotations;
using System.IO.Enumeration;
using CommandLine;
using DependencyUpdated.Core;
using DependencyUpdated.Core.Config;
using DependencyUpdated.Projects.DotNet;
using DependencyUpdated.Repositories.AzureDevOps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;
using ILogger = Serilog.ILogger;

namespace DependencyUpdated
{
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
            var validationResult = config.Value.Validate(new ValidationContext(config.Value)).ToList();
            if (validationResult.Count != 0)
            {
                throw new OptionsValidationException(nameof(UpdaterConfig), typeof(UpdaterConfig),
                    validationResult.Where(x => !string.IsNullOrEmpty(x.ErrorMessage)).Select(x => x.ErrorMessage!));
            }

            var repositoryProvider =
                _serviceProvider.GetRequiredKeyedService<IRepositoryProvider>(config.Value.RepositoryType);
            var repositoryPath = string.IsNullOrEmpty(options.RepositoryPath)
                ? Environment.CurrentDirectory
                : options.RepositoryPath;
            
            Directory.SetCurrentDirectory(repositoryPath);

            foreach (var configEntry in config.Value.Projects)
            {
                repositoryProvider.SwitchToDefaultBranch(repositoryPath);

                var updater = _serviceProvider.GetRequiredKeyedService<IProjectUpdater>(configEntry.Type);
               
                foreach (var project in configEntry.Directories)
                {
                    if (!Path.Exists(project))
                    {
                        throw new FileNotFoundException("Search path not found", project);
                    }
                    var allDepencenciesToUpdate = new List<DependencyDetails>();
                    var projectFiles = updater.GetAllProjectFiles(project).ToArray(); 
                   
                    foreach (var projectFile in projectFiles)
                    {
                        var dependencyToUpdate = await updater.ExtractAllPackagesThatNeedToBeUpdated(projectFile, configEntry);
                        allDepencenciesToUpdate.AddRange(dependencyToUpdate);
                        
                    }

                    if (allDepencenciesToUpdate.Count == 0) //no libs to update, we can skip this project
                    {
                        continue;
                    }

                    var uniqueListOfDependencies = allDepencenciesToUpdate.DistinctBy(x => x.Name).ToList();

                    foreach (var group in configEntry.Groups)
                    {
                        var matchesForGroup = uniqueListOfDependencies
                            .Where(x => FileSystemName.MatchesSimpleExpression(group, x.Name)).ToArray();
                        uniqueListOfDependencies.RemoveAll(x => FileSystemName.MatchesSimpleExpression(group, x.Name));
                        
                        var projectName = configEntry.Name;
                        repositoryProvider.SwitchToUpdateBranch(repositoryPath, projectName, group);
                        
                        var allUpdates = new List<UpdateResult>();
                        foreach (var projectFile in projectFiles)
                        {
                            var updateResults = updater.HandleProjectUpdate(projectFile, matchesForGroup);
                            allUpdates.AddRange(updateResults);
                        }

                        if (allUpdates.Count != 0)
                        {
                             repositoryProvider.CommitChanges(repositoryPath, projectName, group);
                             repositoryProvider.SubmitPullRequest(allUpdates.DistinctBy(x=>x.PackageName).ToArray(), projectName, group).Wait();
                        }
                    }
                }
            }         
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
}