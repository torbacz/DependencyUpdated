using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace DependencyUpdated.Core.UnitTests;

public class ProjectConfigTest : BaseConfigTest
{
    protected override IEnumerable<Tuple<IValidatableObject, IEnumerable<ValidationResult>>> TestCases
    {
        get
        {
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(CreateValidProject(),
                ArraySegment<ValidationResult>.Empty);
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                CreateValidProject() with { Directories = ArraySegment<string>.Empty },
                new[]{ new ValidationResult($"{nameof(Project.Directories)} cannot be empty")});
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                CreateValidProject() with { Directories = ["TestPath"] },
                new[]{ new ValidationResult("Path TestPath not found")});
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                CreateValidProject() with { EachDirectoryAsSeparate = false, Name = string.Empty },
                new[]
                {
                    new ValidationResult(
                        $"{nameof(Project.Name)} must be provided when {nameof(Project.EachDirectoryAsSeparate)} is not set")
                });
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                CreateValidProject() with { EachDirectoryAsSeparate = true, Name = "TestName" },
                new[]
                {
                    new ValidationResult(
                        $"{nameof(Project.Name)} must not be provided when {nameof(Project.EachDirectoryAsSeparate)} is set")
                });
            yield return Tuple.Create<IValidatableObject, IEnumerable<ValidationResult>>(
                CreateValidProject() with { Groups = ArraySegment<string>.Empty },
                new[]{ new ValidationResult($"Missing {nameof(Project.Groups)}.")});
        }
    }

    private static Project CreateValidProject()
    {
        return new Project()
        {
            DependencyConfigurations = new[] { "Test" },
            Directories = new[] { Environment.CurrentDirectory },
            Exclude = new[] { "Exclude" },
            Include = new[] { "Include" },
            Groups = new[] { "Groups" },
            Name = "Name",
            Type = ProjectType.DotNet,
            Version = VersionUpdateType.Major,
            EachDirectoryAsSeparate = false
        };
    }
}