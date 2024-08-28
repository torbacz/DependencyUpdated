using CommandLine;

namespace DependencyUpdated;

public class Options
{
    [Option('c', "configPath", Required = false, HelpText = "Path for the configuration file.")]
    public string? ConfigPath { get; set; }

    [Option('r', "repoPath", Required = false, HelpText = "Path for the repository folder.")]
    public string? RepositoryPath { get; set; }
}