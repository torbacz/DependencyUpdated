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

        var updater = ActivatorUtilities.CreateInstance<Updater>(_serviceProvider);
        await updater.DoUpdate();
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
            .RegisterAzureDevOps()
            .AddOptions<UpdaterConfig>().Bind(_configuration.GetSection("UpdaterConfig"));

        _serviceProvider = services.BuildServiceProvider();
    }
}