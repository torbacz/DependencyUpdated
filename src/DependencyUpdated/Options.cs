using CommandLine;

namespace DependencyUpdated;

public class Options
{
    [Option('c', "configPath", Required = false, HelpText = "Path for the configuration file.")]
    public string? ConfigPath { get; set; }
}