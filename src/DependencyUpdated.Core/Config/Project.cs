using DependencyUpdated.Core.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace DependencyUpdated.Core.Config;

public sealed record Project : IValidatableObject
{
    public ProjectType Type { get; set; }
    
    public VersionUpdateType Version { get; set; }

    public string Name { get; set; } = default!;

    public bool EachDirectoryAsSeparate { get; set; } = false;

    public IReadOnlyList<string> DependencyConfigurations { get; set; } = [];
    
    public IReadOnlyList<string> Directories { get; set; } = [];

    public IReadOnlyList<string> Groups { get; set; } = [];

    public IReadOnlyList<string> Include { get; set; } = [];

    public IReadOnlyList<string> Exclude { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Type))
        {
            yield return new ValidationResult($"Value {Type} is not valid type for {nameof(Type)}");
        }
        
        if (Directories.Count == 0)
        {
            yield return new ValidationResult($"{nameof(Directories)} cannot be empty");
        }

        foreach (var directory in Directories)
        {
            if (!Path.Exists(directory))
            {
                yield return new ValidationResult($"Path {directory} not found"); 
            }
        }

        if (!EachDirectoryAsSeparate && string.IsNullOrEmpty(Name))
        {
            yield return new ValidationResult($"{nameof(Name)} must be provided when {nameof(EachDirectoryAsSeparate)} is not set");
        }
        
        if (EachDirectoryAsSeparate && !string.IsNullOrEmpty(Name))
        {
            yield return new ValidationResult($"{nameof(Name)} must not be provided when {nameof(EachDirectoryAsSeparate)} is set");
        }

        if (Groups.Count == 0)
        {
            yield return new ValidationResult($"Missing {nameof(Groups)}.");
        }
    }

    public void ApplyDefaultValue()
    {
        if (DependencyConfigurations.Count == 0 && Type == ProjectType.DotNet)
        {
            DependencyConfigurations = ["https://api.nuget.org/v3/index.json"];
        }

        if (DependencyConfigurations.Count == 0 && Type == ProjectType.Npm)
        {
            DependencyConfigurations = ["https://registry.npmjs.org"];
        }

        if (Groups.Count == 0)
        {
            Groups = ["*"];
        }
    }
}