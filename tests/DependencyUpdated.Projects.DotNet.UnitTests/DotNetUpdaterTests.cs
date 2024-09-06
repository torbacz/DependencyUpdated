using DependencyUpdated.Core;
using DependencyUpdated.Core.Config;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Serilog;
using Xunit;

namespace DependencyUpdated.Projects.DotNet.UnitTests;

public class DotNetUpdaterTests
{
    private readonly DotNetUpdater _target;
    private readonly ILogger _logger;
    private readonly IMemoryCache _memoryCahce;
    private readonly string _searchPath;

    public DotNetUpdaterTests()
    {
        _logger = Substitute.For<ILogger>();
        _memoryCahce = Substitute.For<IMemoryCache>();
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
    public async Task ExtractAllPackagesThatNeedToBeUpdated_Should_UpdatePatchVersion()
    {
        // Arrange
        var path = Path.Combine("Projects", "SampleProject.csproj");
        var config = new Project() { Version = VersionUpdateType.Patch, Type = ProjectType.DotNet };
        config.ApplyDefaultValue();
        var expectedResult = new List<DependencyDetails>()
        {
            new("Serilog", new Version(3,0,1, 0))
        };
        
        // Act
        var packages = await _target.ExtractAllPackagesThatNeedToBeUpdated(new[] { path }, config);
        
        // Assert
        using (new AssertionScope())
        {
            packages.Should().BeEquivalentTo(expectedResult);
        }
    }
    
    [Fact]
    public async Task ExtractAllPackagesThatNeedToBeUpdated_Should_UpdateMinorVersion()
    {
        // Arrange
        var path = Path.Combine("Projects", "SampleProject.csproj");
        var config = new Project() { Version = VersionUpdateType.Minor, Type = ProjectType.DotNet };
        config.ApplyDefaultValue();
        var expectedResult = new List<DependencyDetails>()
        {
            new("Serilog", new Version(3,1,1, 0))
        };
        
        // Act
        var packages = await _target.ExtractAllPackagesThatNeedToBeUpdated(new[] { path }, config);
        
        // Assert
        using (new AssertionScope())
        {
            packages.Should().BeEquivalentTo(expectedResult);
        }
    }
    
    [Fact]
    public async Task ExtractAllPackagesThatNeedToBeUpdated_Should_UpdateMajorVersion()
    {
        // Arrange
        var path = Path.Combine("Projects", "SampleProject.csproj");
        var config = new Project() { Version = VersionUpdateType.Major, Type = ProjectType.DotNet };
        config.ApplyDefaultValue();
        var expectedResult = new List<DependencyDetails>()
        {
            new("Serilog", new Version(4,0,1, 0))
        };
        
        // Act
        var packages = await _target.ExtractAllPackagesThatNeedToBeUpdated(new[] { path }, config);
        
        // Assert
        using (new AssertionScope())
        {
            packages.Should().BeEquivalentTo(expectedResult);
        }
    }
}
