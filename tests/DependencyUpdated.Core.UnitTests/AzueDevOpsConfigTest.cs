using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace DependencyUpdated.Core.UnitTests;

public class AzureDevOpsConfigTest : BaseConfigTest
{
    protected override IEnumerable<Tuple<IValidatableObject, IEnumerable<ValidationResult>>> TestCases
    {
        get
        {
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(CreateValidConfig(),
                ArraySegment<ValidationResult>.Empty);
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                CreateValidConfig() with { Username = string.Empty },
                new[]
                {
                    new ValidationResult(
                        $"{nameof(AzureDevOpsConfig.Username)} must be provided in {nameof(AzureDevOpsConfig)}")
                });
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                CreateValidConfig() with { Email = string.Empty },
                new[]
                {
                    new ValidationResult(
                        $"{nameof(AzureDevOpsConfig.Email)} must be provided in {nameof(AzureDevOpsConfig)}")
                });
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                CreateValidConfig() with { Organization = string.Empty },
                new[]
                {
                    new ValidationResult(
                        $"{nameof(AzureDevOpsConfig.Organization)} must be provided in {nameof(AzureDevOpsConfig)}")
                });
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                CreateValidConfig() with { Project = string.Empty },
                new[]
                {
                    new ValidationResult(
                        $"{nameof(AzureDevOpsConfig.Project)} must be provided in {nameof(AzureDevOpsConfig)}")
                });
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                CreateValidConfig() with { Repository = string.Empty },
                new[]
                {
                    new ValidationResult(
                        $"{nameof(AzureDevOpsConfig.Repository)} must be provided in {nameof(AzureDevOpsConfig)}")
                });
        }
    }

    private static AzureDevOpsConfig CreateValidConfig()
    {
        return new AzureDevOpsConfig()
        {
            Username = "User",
            Email = "Email",
            Project = "Project",
            Organization = "Organization",
            Repository = "Repository",
            AutoComplete = true,
            BranchName = "Branch",
            PAT = "PAT",
            TargetBranchName = "TargetBranch",
            WorkItemId = 1500
        };
    }
}