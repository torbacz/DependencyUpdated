using DependencyUpdated.Core.Models;

namespace DependencyUpdated.Repositories.AzureDevOps.UnitTests;

public class PullRequestDescriptionTests
{
    [Fact]
    public void Create_Should_IncludeAllSuppliedUpdates()
    {
        var updates = new[]
        {
            new UpdateResult("Package.A", "1.0.0", "2.0.0"),
            new UpdateResult("Package.A", "1.0.0", "2.0.0"),
            new UpdateResult("Package.B", "1.0.0", "2.0.0")
        };

        var description = AzureDevOps.CreatePrDescription(updates);

        description.Should().Contain("Packages:");
        description.Should().Contain("- Package.A");
        description.Should().Contain("- Package.B");
        (description.Split("Package.A", StringSplitOptions.None).Length - 1).Should().Be(2);
        description.Should().NotContain("Log:");
    }

    [Fact]
    public void Create_Should_TrimDescriptionToAzureDevOpsLimit()
    {
        var updates = Enumerable.Range(1, 1_000)
            .Select(index => new UpdateResult($"Package.{index:D4}", "1.0.0", "2.0.0"))
            .ToArray();

        var description = AzureDevOps.CreatePrDescription(updates);

        description.Length.Should().BeLessThanOrEqualTo(4_000);
        description.Should().EndWith("...");
    }
}
