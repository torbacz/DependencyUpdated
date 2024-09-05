using System.ComponentModel.DataAnnotations;

namespace DependencyUpdated.Core.Config;

public sealed class Project : IValidatableObject
{
    public ProjectType Type { get; set; }
    
    public VersionUpdateType Version { get; set; }

    public string Name { get; set; } = default!;

    public string[] DependencyConfigurations { get; set; } = [];
    
    public string[] Directories { get; set; } = [];

    public string[] Groups { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enum.IsDefined(Type))
        {
            yield return new ValidationResult($"Value {Type} is not valid type for {nameof(Type)}");
        }
        
        if (Directories.Length == 0)
        {
            yield return new ValidationResult($"{nameof(Directories)} cannot be empty");
        }

        if (string.IsNullOrEmpty(Name))
        {
            yield return new ValidationResult($"{nameof(Name)} must be provided");
        }

        if (Groups.Length == 0)
        {
            yield return new ValidationResult($"Missing ${nameof(Groups)}.");
        }
    }

    public void ApplyDefaultValue()
    {
        if (DependencyConfigurations.Length == 0 && Type == ProjectType.DotNet)
        {
            DependencyConfigurations = ["https://api.nuget.org/v3/index.json"];
        }

        if (Groups.Length == 0)
        {
            Groups = ["*"];
        }
    }
}