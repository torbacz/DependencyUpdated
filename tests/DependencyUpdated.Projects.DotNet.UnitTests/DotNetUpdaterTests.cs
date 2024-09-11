using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Core.Models.Enums;
using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using Serilog;
using Xunit;

namespace DependencyUpdated.Projects.DotNet.UnitTests;

public class DotNetUpdaterTests
{
    private readonly DotNetUpdater _target;
    private readonly ILogger _logger;
    private readonly MockMemoryCache _memoryCahce;
    private readonly string _searchPath;

    public DotNetUpdaterTests()
    {
        _logger = Substitute.For<ILogger>();
        _memoryCahce = new MockMemoryCache();
        _target = new DotNetUpdater(_logger, _memoryCahce);
        _searchPath = "Projects";
    }
    
    [Fact]
    public void GetAllProjectFiles_Should_ReturnAllProjects()
    {
        // Arrange
        var expectedResult = new[] { Path.Combine("Projects", "SampleProject.csproj") };
        
        // Act
        var result = _target.GetAllProjectFiles(_searchPath);
        
        // Assert
        using (new AssertionScope())
        {
            result.Count.Should().Be(1);
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
        var expectedResult = new List<DependencyDetails>()
        {
            new("Serilog", new Version(3,0,0, 0))
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
    public async Task GetVersions_Should_ReturnVersionFromCache()
    {
        // Arrange
        var packages = new DependencyDetails("TestName", new Version(1, 0, 0));
        var projectConfiguration = new Project();
        var cacheData = new List<DependencyDetails> { new("TestName", new Version(1, 2, 3)) };
        _memoryCahce.AddEntry(packages.Name, cacheData);

        // Act
        var result = await _target.GetVersions(packages, projectConfiguration);

        // Assert
        using (new AssertionScope())
        {
            result.Should().BeEquivalentTo(cacheData);
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
    public async Task GetVersions_Should_GetPackagesFromSource()
    {
        // Arrange
        var packages = new DependencyDetails("Serilog", new Version(1, 0, 0));
        var projectConfiguration = new Project() { Type = ProjectType.DotNet };
        projectConfiguration.ApplyDefaultValue();
        projectConfiguration.DependencyConfigurations = projectConfiguration.DependencyConfigurations
            .Concat(new[] { "./Projects/Nuget.config" }).ToArray();

        // Act
        var result = await _target.GetVersions(packages, projectConfiguration);

        // Assert
        using (new AssertionScope())
        {
            result.Should().NotBeNullOrEmpty();
            result.Should().ContainEquivalentOf(new DependencyDetails("Serilog", new Version(4, 0, 1, 0)));
        }
    }

    [Fact]
    public async Task UpdateCsProj_Should_UpdateVersion()
    {
        // Arrange
        var projectToUpdate = "testProj.csproj";
        if (File.Exists(projectToUpdate))
        {
            File.Delete(projectToUpdate);
        }
        File.Copy("./Projects/SampleProject.csproj", projectToUpdate);
        
        // Act
        var result = _target.HandleProjectUpdate([projectToUpdate],
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
