using System.ComponentModel.DataAnnotations;

namespace DependencyUpdated.Core.Config;

public sealed class Project : IValidatableObject
{
    private string[] _groups = ["*"];
    
    public ProjectType Type { get; set; }

    public string Name { get; set; } = default!;
    
    public string[] DependencyConfigurations { get; set; } = ArraySegment<string>.Empty.ToArray();
    
    public string[] Directories { get; set; } = ArraySegment<string>.Empty.ToArray();

    public string[] Groups
    {
        get => _groups;
        set => _groups = value;
    }

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
}