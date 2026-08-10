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
    public async Task ExtractAllPackages_Should_IgnoreInvalidVersions()
    {
        var path = Path.GetTempFileName();
        try
        {
            var packageJson = """
                {
                  "dependencies": {
                    "stable-package": "^1.2.3",
                    "pre-release-package": "1.0.0-beta",
                    "invalid-range-package": "1.0~2"
                  },
                  "devDependencies": {}
                }
                """;
            await File.WriteAllTextAsync(path, packageJson);

            var packages = await _target.ExtractAllPackages([path]);

            packages.Should().BeEquivalentTo([new DependencyDetails("stable-package", new Version(1, 2, 3))]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExtractAllPackages_Should_ReturnEmptyForNullPackage()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "null");

            var packages = await _target.ExtractAllPackages([path]);

            packages.Should().BeEmpty();
        }
        finally
        {
            File.Delete(path);
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
    public async Task GetVersions_Should_ThrowForMissingDependencyConfiguration()
    {
        var config = new Project { Type = ProjectType.Npm };
        var dependency = new DependencyDetails("@angular/core", new Version(8, 2, 14));

        await _target.Awaiting(x => x.GetVersions(dependency, config)).Should()
            .ThrowExactlyAsync<InvalidOperationException>();
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
        var updateResult = _target.HandleProjectUpdate(config, [projectToUpdate], depsToUpdate);

        // Assert
        using (new AssertionScope())
        {
            updateResult.Should().NotBeNullOrEmpty();
            updateResult.Should().ContainEquivalentOf(new UpdateResult(depsToUpdate[0].Name, "8.2.14", "9.0.0"));
        }
    }

    [Fact]
    public void HandleProjectUpdate_Should_ReturnNoUpdatesForMissingDependency()
    {
        var config = new Project { Type = ProjectType.Npm };
        var dependency = new DependencyDetails("missing-package", new Version(2, 0, 0));

        var updates = _target.HandleProjectUpdate(config, [Path.Combine(_searchPath, "package.json")], [dependency]);

        updates.Should().BeEmpty();
    }

    [Fact]
    public void GetAllProjectFiles_Should_ReturnAllProjects()
    {
        // Arrange
        var expectedResult = new[] { Path.Combine(_searchPath, "package.json") };
        
        // Act
        var result = _target.GetAllProjectFiles(_searchPath);
        
        // Assert
        using (new AssertionScope())
        {
            result.Should().BeEquivalentTo(expectedResult);
        }
    }
}
