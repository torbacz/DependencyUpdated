using System.ComponentModel.DataAnnotations;

namespace DependencyUpdated.Core.Config;

public class AzureDevOpsConfig : IValidatableObject
{
    public string? Username { get; set; }

    public string? Email { get; set; }
    
    public string? PAT { get; set; }
    
    public string? Organization { get; set; }
    
    public string? Project { get; set; }

    public string? Repository { get; set; }
    
    public int? WorkItemId { get; set; }

    public string TargetBranchName { get; set; } = "dev";

    public string BranchName { get; set; } = "updateDependencies";

    public bool AutoComplete { get; set; } = true;
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrEmpty(Username))
        {
            yield return new ValidationResult($"{nameof(Username)} must be provided in {nameof(AzureDevOpsConfig)}");
        }
        
        if (string.IsNullOrEmpty(Email))
        {
            yield return new ValidationResult($"{nameof(Email)} must be provided in {nameof(AzureDevOpsConfig)}");
        }
        
        if (string.IsNullOrEmpty(Organization))
        {
            yield return new ValidationResult($"{nameof(Organization)} must be provided in {nameof(AzureDevOpsConfig)}");
        }
        
        if (string.IsNullOrEmpty(Project))
        {
            yield return new ValidationResult($"{nameof(Project)} must be provided in {nameof(AzureDevOpsConfig)}");
        }
        
        if (string.IsNullOrEmpty(Repository))
        {
            yield return new ValidationResult($"{nameof(Repository)} must be provided in {nameof(AzureDevOpsConfig)}");
        }
    }
}