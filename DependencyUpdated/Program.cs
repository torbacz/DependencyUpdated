using System.ComponentModel.DataAnnotations;
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

        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(RunApplication);
        }

        private static void RunApplication(Options options)
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
                var updater = _serviceProvider.GetRequiredKeyedService<IProjectUpdater>(configEntry.Type);
                foreach (var project in configEntry.Directories)
                {
                    repositoryProvider.SwitchToDefaultBranch(repositoryPath);
                    var projectName = configEntry.Name;
                    repositoryProvider.SwitchToUpdateBranch(repositoryPath, projectName);
                  
                    var updates = updater.UpdateProject(project, configEntry).Result;

                    if (updates.Count == 0)
                    {
                        continue;
                    }

                    repositoryProvider.CommitChanges(repositoryPath, projectName);
                    repositoryProvider.SubmitPullRequest(updates, projectName).Wait();
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