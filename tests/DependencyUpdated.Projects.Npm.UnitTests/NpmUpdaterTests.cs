using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Core.Models.Enums;
using FluentAssertions;
using FluentAssertions.Execution;

namespace DependencyUpdated.Projects.Npm.UnitTests;

public class NpmUpdaterTests
{
    private readonly NpmUpdater _target = new();
    private readonly string _searchPath = "Projects";

    [Fact]
    public async Task ExtractAllPackages_Should_ReturnPackagesFromPackagesJsonFile()
    {
        // Arrange
        var path = Path.Combine(_searchPath, "package.json");
        var config = new Project() { Version = VersionUpdateType.Patch, Type = ProjectType.Npm };
        config.ApplyDefaultValue();
        var expectedResult = new List<DependencyDetails>()
        {
            new("@angular/core", new Version(8,2,14)),
            new("@angular/cli", new Version(8,3,29))

        };
        
        // Act
        var packages = await _target.ExtractAllPackages(new[] { path });
        
        // Assert
        using (new AssertionScope())
        {
            packages.Should().BeEquivalentTo(expectedResult);
        }
    }
}