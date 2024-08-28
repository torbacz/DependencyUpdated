using System.ComponentModel.DataAnnotations;

namespace DependencyUpdated.Core.Config;

public sealed class UpdaterConfig : IValidatableObject
{
    public RepositoryType RepositoryType { get; set; }

    public AzureDevOpsConfig AzureDevOps { get; set; } = new();
    
    public List<Project> Projects { get; set; } = new();
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Projects.Count == 0)
        {
            yield return new ValidationResult($"At least one {nameof(Projects)} must be provided.");
        }
        
        if (!Enum.IsDefined(RepositoryType))
        {
            yield return new ValidationResult($"Value {RepositoryType} is not valid type for {nameof(RepositoryType)}");
        }
        
        var projectsNamesDistinctCount = Projects.DistinctBy(x => x.Name).Count();
        if (projectsNamesDistinctCount != Projects.Count)
        {
            yield return new ValidationResult("Projects must contains unique names");
        }

        if (RepositoryType == RepositoryType.AzureDevOps)
        {
            var validationResult = AzureDevOps.Validate(new ValidationContext(AzureDevOps));
            foreach (var result in validationResult)
            {
                yield return result;
            }
        }

        foreach (var entry in Projects)
        {
            var validationResult = entry.Validate(new ValidationContext(entry));
            foreach (var result in validationResult)
            {
                yield return result;
            }
        }
    }
}