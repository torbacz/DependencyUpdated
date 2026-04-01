using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace DependencyUpdated.Core.UnitTests;

public class UpdaterConfigTest : BaseConfigTest
{
    protected override IEnumerable<Tuple<IValidatableObject, IEnumerable<ValidationResult>>> TestCases
    {
        get
        {
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                CreateValidConfig(),
                ArraySegment<ValidationResult>.Empty);

            var emptyProjectsConfig = CreateValidConfig();
            emptyProjectsConfig.Projects = [];
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                emptyProjectsConfig,
                [new ValidationResult($"At least one {nameof(UpdaterConfig.Projects)} must be provided.")]);

            var invalidRepositoryTypeConfig = CreateValidConfig();
            invalidRepositoryTypeConfig.RepositoryType = (RepositoryType)999;
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                invalidRepositoryTypeConfig,
                [new ValidationResult($"Value {(RepositoryType)999} is not valid type for {nameof(UpdaterConfig.RepositoryType)}")]);

            var duplicateProjectNamesConfig = CreateValidConfig();
            duplicateProjectNamesConfig.Projects =
            [
                CreateValidProject("SharedName", false),
                CreateValidProject("SharedName", false)
            ];
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                duplicateProjectNamesConfig,
                [new ValidationResult("Projects must contains unique names")]);

            var eachDirectoryProjectsConfig = CreateValidConfig();
            eachDirectoryProjectsConfig.Projects =
            [
                CreateValidProject(string.Empty, true),
                CreateValidProject(string.Empty, true)
            ];
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                eachDirectoryProjectsConfig,
                ArraySegment<ValidationResult>.Empty);

            var invalidAzureDevOpsConfig = CreateValidConfig();
            invalidAzureDevOpsConfig.AzureDevOps = CreateValidAzureConfig() with { Username = string.Empty };
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                invalidAzureDevOpsConfig,
                [new ValidationResult($"{nameof(AzureDevOpsConfig.Username)} must be provided in {nameof(AzureDevOpsConfig)}")]);

            var invalidRepoTypeNoAzureValidationConfig = CreateValidConfig();
            invalidRepoTypeNoAzureValidationConfig.RepositoryType = (RepositoryType)999;
            invalidRepoTypeNoAzureValidationConfig.AzureDevOps = CreateValidAzureConfig() with { Username = string.Empty };
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                invalidRepoTypeNoAzureValidationConfig,
                [new ValidationResult($"Value {(RepositoryType)999} is not valid type for {nameof(UpdaterConfig.RepositoryType)}")]);

            var invalidProjectConfig = CreateValidConfig();
            invalidProjectConfig.Projects =
            [
                CreateValidProject("Name", false) with
                {
                    Directories = []
                }
            ];
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                invalidProjectConfig,
                [new ValidationResult($"{nameof(Project.Directories)} cannot be empty")]);
        }
    }

    [Fact]
    public void ApplyDefaultValues_Should_ApplyDefaultsOnEachProject()
    {
        // Arrange
        var config = CreateValidConfig();
        config.Projects =
        [
            CreateValidProject("DotNetProject", false) with
            {
                Type = ProjectType.DotNet,
                DependencyConfigurations = [],
                Groups = []
            },
            CreateValidProject("NpmProject", false) with
            {
                Type = ProjectType.Npm,
                DependencyConfigurations = [],
                Groups = []
            }
        ];

        // Act
        config.ApplyDefaultValues();

        // Assert
        using (new AssertionScope())
        {
            config.Projects[0].DependencyConfigurations.Should().BeEquivalentTo("https://api.nuget.org/v3/index.json");
            config.Projects[1].DependencyConfigurations.Should().BeEquivalentTo("https://registry.npmjs.org");
            config.Projects[0].Groups.Should().BeEquivalentTo("*");
            config.Projects[1].Groups.Should().BeEquivalentTo("*");
        }
    }

    private static UpdaterConfig CreateValidConfig()
    {
        return new UpdaterConfig()
        {
            RepositoryType = RepositoryType.AzureDevOps,
            AzureDevOps = CreateValidAzureConfig(),
            Projects = [CreateValidProject("ProjectName", false)]
        };
    }

    private static AzureDevOpsConfig CreateValidAzureConfig()
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

    private static Project CreateValidProject(string name, bool eachDirectoryAsSeparate)
    {
        return new Project()
        {
            DependencyConfigurations = ["Test"],
            Directories = [Environment.CurrentDirectory],
            Exclude = ["Exclude"],
            Include = ["Include"],
            Groups = ["Groups"],
            Name = name,
            Type = ProjectType.DotNet,
            Version = VersionUpdateType.Major,
            EachDirectoryAsSeparate = eachDirectoryAsSeparate
        };
    }
}
