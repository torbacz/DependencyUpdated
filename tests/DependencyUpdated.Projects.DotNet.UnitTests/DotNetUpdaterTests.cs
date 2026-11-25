using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace DependencyUpdated.Projects.DotNet.UnitTests;

public class DotNetUpdaterTests
{
    private readonly IProjectUpdater _target;
    private readonly string _searchPath = "Projects";

    public DotNetUpdaterTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.RegisterDotNetServices();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        _target = serviceProvider.GetRequiredKeyedService<IProjectUpdater>(ProjectType.DotNet);
    }
    
    [Fact]
    public void GetAllProjectFiles_Should_ReturnAllProjects()
    {
        // Arrange
        var expectedResult = new[] { Path.Combine("Projects", "SampleProject.csproj"), Path.Combine("Projects", "Directory.Build.props") };
        
        // Act
        var result = _target.GetAllProjectFiles(_searchPath);
        
        // Assert
        using (new AssertionScope())
        {
            result.Count.Should().Be(2);
            result.Should().ContainInOrder(expectedResult);
        }
    }

    [Fact]
    public async Task ExtractAllPackages_Should_ReturnPackagesFromCsProjFile()
    {
        // Arrange
        var path = Path.Combine("Projects", "SampleProject.csproj");
        var config = new Project() { Version = VersionUpdateType.Patch, Type = ProjectType.DotNet };
        config.ApplyDefaultValue();
        var expectedResult = new List<DependencyDetails>() { new("Serilog", new Version(3, 0, 0, 0)) };

        // Act
        var packages = await _target.ExtractAllPackages([path]);

        // Assert
        using (new AssertionScope())
        {
            packages.Should().BeEquivalentTo(expectedResult);
        }
    }
    
    [Fact]
    public async Task ExtractAllPackages_Should_ReturnPackagesFromDirectoryBuildPropsFile()
    {
        // Arrange
        var path = Path.Combine("Projects", "Directory.Build.props");
        var config = new Project() { Version = VersionUpdateType.Patch, Type = ProjectType.DotNet };
        config.ApplyDefaultValue();
        var expectedResult = new List<DependencyDetails>() { new("Microsoft.CodeAnalysis.CSharp.CodeStyle", new Version(4, 12, 0, 0)) };

        // Act
        var packages = await _target.ExtractAllPackages([path]);

        // Assert
        using (new AssertionScope())
        {
            packages.Should().BeEquivalentTo(expectedResult);
        }
    }

    [Fact]
    public async Task GetVersions_Should_ThrowForMissingDependencyConfiguration()
    {
        // Arrange
        var packages = new DependencyDetails("TestName", new Version(1, 0, 0));
        var projectConfiguration = new Project();

        // Act, Assert
        await _target.Awaiting(x => x.GetVersions(packages, projectConfiguration)).Should()
            .ThrowExactlyAsync<InvalidOperationException>();
    }
    
    [Fact]
    public async Task GetVersions_Should_UpdateProjectToLatestPackageFromSource()
    {
        // Arrange
        var packages = new DependencyDetails("Serilog", new Version(1, 0, 0));
        var projectConfiguration = new Project() { Type = ProjectType.DotNet };
        projectConfiguration.ApplyDefaultValue();
        projectConfiguration.DependencyConfigurations = projectConfiguration.DependencyConfigurations
            .Concat(new[] { "./Projects/nuget.config" }).ToArray();
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryDirectory);
        var projectFilePath = Path.Combine(temporaryDirectory, "TestProject.csproj");
        await File.WriteAllTextAsync(projectFilePath,
            "<Project><ItemGroup><PackageReference Include=\"Serilog\" Version=\"1.0.0\" /></ItemGroup></Project>");

        try
        {
            // Act
            var availableVersions = await _target.GetVersions(packages, projectConfiguration);
            var latestPackage = availableVersions.OrderByDescending(x => x.Version).First();
            var updateResults = _target.HandleProjectUpdate(projectConfiguration, [projectFilePath], [latestPackage]);
            var updatedPackages = await _target.ExtractAllPackages([projectFilePath]);

            // Assert
            using (new AssertionScope())
            {
                availableVersions.Should().NotBeNullOrEmpty();
                updateResults.Should().ContainEquivalentOf(new UpdateResult("Serilog", "1.0.0", latestPackage.Version.ToString()));
                updatedPackages.Should().ContainEquivalentOf(latestPackage);
            }
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateCsProj_Should_UpdateVersion()
    {
        // Arrange
        var projectConfiguration = new Project() { Type = ProjectType.DotNet };
        var projectToUpdate = "testProj.csproj";
        if (File.Exists(projectToUpdate))
        {
            File.Delete(projectToUpdate);
        }

        File.Copy("./Projects/SampleProject.csproj", projectToUpdate);

        // Act
        var result = _target.HandleProjectUpdate(projectConfiguration, [projectToUpdate],
            new List<DependencyDetails>() { new("Serilog", new Version(4, 0, 0)) });

        // Assert
        using (new AssertionScope())
        {
            result.Should().ContainEquivalentOf(new UpdateResult("Serilog", "3.0.0", "4.0.0"));
            var packagesFromfile = await _target.ExtractAllPackages([projectToUpdate]);
            packagesFromfile.Should().ContainEquivalentOf(new DependencyDetails("Serilog", new Version(4, 0, 0, 0)));
            File.Delete(projectToUpdate);
        }
    }
}
