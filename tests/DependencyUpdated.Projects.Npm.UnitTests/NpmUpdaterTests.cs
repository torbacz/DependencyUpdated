using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace DependencyUpdated.Projects.Npm.UnitTests;

public class NpmUpdaterTests
{
    private readonly IProjectUpdater _target;
    private readonly string _searchPath = "Projects";

    public NpmUpdaterTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.RegisterNpmServices();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        _target = serviceProvider.GetRequiredKeyedService<IProjectUpdater>(ProjectType.Npm);
    }

    [Fact]
    public async Task ExtractAllPackages_Should_ReturnPackagesFromPackagesJsonFile()
    {
        // Arrange
        var path = Path.Combine(_searchPath, "package.json");
        var config = new Project() { Version = VersionUpdateType.Patch, Type = ProjectType.Npm };
        config.ApplyDefaultValue();
        var expectedResult = new List<DependencyDetails>()
        {
            new("@angular/core", new Version(8, 2, 14)), new("@angular/cli", new Version(8, 3, 29))
        };

        // Act
        var packages = await _target.ExtractAllPackages(new[] { path });

        // Assert
        using (new AssertionScope())
        {
            packages.Should().BeEquivalentTo(expectedResult);
        }
    }

    [Fact]
    public async Task GetVersions_Should_ReturnVersions()
    {
        // Arrange
        var config = new Project() { Version = VersionUpdateType.Patch, Type = ProjectType.Npm };
        config.ApplyDefaultValue();
        var dependency = new DependencyDetails("@angular/core", new Version(8, 2, 14));

        // Act
        var versions = await _target.GetVersions(dependency, config);

        // Assert
        using (new AssertionScope())
        {
            versions.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void HandleProjectUpdate_Should_UpdateProjectFile()
    {
        // Arrange
        var projectToUpdate = "./package.json";
        if (File.Exists(projectToUpdate))
        {
            File.Delete(projectToUpdate);
        }

        File.Copy($"./{_searchPath}/package.json", projectToUpdate);

        var config = new Project() { Version = VersionUpdateType.Patch, Type = ProjectType.Npm };
        config.ApplyDefaultValue();
        var depsToUpdate = new List<DependencyDetails>() { new("@angular/core", new Version(9, 0, 0)), };

        // Act
        var updateResult = _target.HandleProjectUpdate([projectToUpdate], depsToUpdate);

        // Assert
        using (new AssertionScope())
        {
            updateResult.Should().NotBeNullOrEmpty();
            updateResult.Should().ContainEquivalentOf(new UpdateResult(depsToUpdate[0].Name, "8.2.14", "9.0.0"));
        }
    }
}