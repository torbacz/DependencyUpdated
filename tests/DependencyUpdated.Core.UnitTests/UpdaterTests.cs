using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Core.Models.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace DependencyUpdated.Core.UnitTests;

public class UpdaterTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IProjectUpdater _projectUpdater;
    private readonly IRepositoryProvider _repositoryProvider;
    private readonly IOptions<UpdaterConfig> _config;
    private readonly ILogger _logger;
    private readonly Updater _target;
    private readonly string _currentDir;
    private readonly IMemoryCache _memoryCache;

    public UpdaterTests()
    {
        _config = new OptionsWrapper<UpdaterConfig>(new UpdaterConfig()
        {
            RepositoryType = RepositoryType.AzureDevOps,
            Projects =
            [
                new()
                {
                    Name = "TestProjectName",
                    Version = VersionUpdateType.Major,
                    Directories = ["TestDir"],
                    Type = ProjectType.DotNet,
                }
            ]
        });
        _config.Value.ApplyDefaultValues();
        _currentDir = Environment.CurrentDirectory;
        _repositoryProvider = Substitute.For<IRepositoryProvider>();
        _projectUpdater = Substitute.For<IProjectUpdater>();
        _serviceProvider = new ServiceCollection()
            .AddKeyedSingleton(_config.Value.Projects[0].Type, _projectUpdater)
            .AddKeyedSingleton(_config.Value.RepositoryType, _repositoryProvider)
            .BuildServiceProvider();
        _logger = Substitute.For<ILogger>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _target = new Updater(_serviceProvider, _config, _logger, _memoryCache);
    }

    [Fact]
    public async Task Update_Should_UpdateOnlyMinorVersion()
    {
        // Arrange
        _config.Value.Projects[0].Version = VersionUpdateType.Minor;
        var projectList = new List<string>() { "TestProjectFile" };
        _projectUpdater
            .GetAllProjectFiles(_config.Value.Projects[0].Directories[0])
            .Returns(projectList);
        var projectDependencies =
            new List<DependencyDetails>() { new("TestDependency", new Version(1, 0, 0)), };
        _projectUpdater.ExtractAllPackages(projectList).Returns(projectDependencies);
        _projectUpdater.GetVersions(projectDependencies[0], _config.Value.Projects[0])
            .Returns(new List<DependencyDetails>
            {
                new(projectDependencies[0].Name, new Version(2, 0, 0)),
                new(projectDependencies[0].Name, new Version(1, 1, 0)),
                new(projectDependencies[0].Name, new Version(1, 0, 2)),
            });

        var expectedDependencyUpdate = new List<DependencyDetails>(new[]
        {
            new DependencyDetails(projectDependencies[0].Name, new Version(1, 1, 0))
        });
        var expectedUpdateResult = new List<UpdateResult> { new(projectDependencies[0].Name, "1.0.0", "1.1.0") };
        _projectUpdater
            .HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)))
            .Returns(expectedUpdateResult);

        // Act
        await _target.DoUpdate();

        // Assert
        using (new AssertionScope())
        {
            _projectUpdater.Received(1).HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)));
            _repositoryProvider.Received(1).CommitChanges(_currentDir, _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
            await _repositoryProvider.Received(1).SubmitPullRequest(expectedUpdateResult,
                _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
        }
    }

    [Fact]
    public async Task Update_Should_UpdateOnlyPatchVersion()
    {
        // Arrange
        _config.Value.Projects[0].Version = VersionUpdateType.Patch;
        var projectList = new List<string>() { "TestProjectFile" };
        _projectUpdater
            .GetAllProjectFiles(_config.Value.Projects[0].Directories[0])
            .Returns(projectList);
        var projectDependencies =
            new List<DependencyDetails>() { new("TestDependency", new Version(1, 0, 0)), };
        _projectUpdater.ExtractAllPackages(projectList).Returns(projectDependencies);
        _projectUpdater.GetVersions(projectDependencies[0], _config.Value.Projects[0])
            .Returns(new List<DependencyDetails>
            {
                new(projectDependencies[0].Name, new Version(2, 0, 0)),
                new(projectDependencies[0].Name, new Version(1, 1, 0)),
                new(projectDependencies[0].Name, new Version(1, 0, 2)),
            });

        var expectedDependencyUpdate = new List<DependencyDetails>(new[]
        {
            new DependencyDetails(projectDependencies[0].Name, new Version(1, 0, 2))
        });
        var expectedUpdateResult = new List<UpdateResult> { new(projectDependencies[0].Name, "1.0.0", "1.0.2") };
        _projectUpdater
            .HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)))
            .Returns(expectedUpdateResult);

        // Act
        await _target.DoUpdate();

        // Assert
        using (new AssertionScope())
        {
            _projectUpdater.Received(1).HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)));
            _repositoryProvider.Received(1).CommitChanges(_currentDir, _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
            await _repositoryProvider.Received(1).SubmitPullRequest(expectedUpdateResult,
                _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
        }
    }

    [Fact]
    public async Task Update_Should_UpdateMajorVersion()
    {
        // Arrange
        _config.Value.Projects[0].Version = VersionUpdateType.Major;
        var projectList = new List<string>() { "TestProjectFile" };
        _projectUpdater
            .GetAllProjectFiles(_config.Value.Projects[0].Directories[0])
            .Returns(projectList);
        var projectDependencies =
            new List<DependencyDetails>() { new("TestDependency", new Version(1, 0, 0)), };
        _projectUpdater.ExtractAllPackages(projectList).Returns(projectDependencies);
        _projectUpdater.GetVersions(projectDependencies[0], _config.Value.Projects[0])
            .Returns(new List<DependencyDetails>
            {
                new(projectDependencies[0].Name, new Version(2, 0, 0)),
                new(projectDependencies[0].Name, new Version(1, 1, 0)),
                new(projectDependencies[0].Name, new Version(1, 0, 2)),
            });

        var expectedDependencyUpdate = new List<DependencyDetails>(new[]
        {
            new DependencyDetails(projectDependencies[0].Name, new Version(2, 0, 0))
        });
        var expectedUpdateResult = new List<UpdateResult> { new(projectDependencies[0].Name, "1.0.0", "2.0.0") };
        _projectUpdater
            .HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)))
            .Returns(expectedUpdateResult);

        // Act
        await _target.DoUpdate();

        // Assert
        using (new AssertionScope())
        {
            _projectUpdater.Received(1).HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)));
            _repositoryProvider.Received(1).CommitChanges(_currentDir, _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
            await _repositoryProvider.Received(1).SubmitPullRequest(expectedUpdateResult,
                _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
        }
    }

    [Fact]
    public async Task Update_Should_FilterGroups()
    {
        // Arrange
        _config.Value.Projects[0].Version = VersionUpdateType.Major;
        _config.Value.Projects[0].Groups = new List<string>() { "Test.*" };
        var projectList = new List<string>() { "TestProjectFile" };
        _projectUpdater
            .GetAllProjectFiles(_config.Value.Projects[0].Directories[0])
            .Returns(projectList);
        var projectDependencies =
            new List<DependencyDetails>()
            {
                new("TestDependency", new Version(1, 0, 0)), new("Test.Dependency", new Version(1, 0, 0)),
            };
        _projectUpdater.ExtractAllPackages(projectList).Returns(projectDependencies);
        _projectUpdater.GetVersions(projectDependencies[0], _config.Value.Projects[0])
            .Returns(new List<DependencyDetails>
            {
                new(projectDependencies[0].Name, new Version(2, 0, 0)),
                new(projectDependencies[0].Name, new Version(1, 1, 0)),
                new(projectDependencies[0].Name, new Version(1, 0, 2)),
            });
        _projectUpdater.GetVersions(projectDependencies[1], _config.Value.Projects[0])
            .Returns(new List<DependencyDetails>
            {
                new(projectDependencies[1].Name, new Version(2, 0, 0)),
                new(projectDependencies[1].Name, new Version(1, 1, 0)),
                new(projectDependencies[1].Name, new Version(1, 0, 2)),
            });

        var expectedDependencyUpdate = new List<DependencyDetails>(new[]
        {
            new DependencyDetails(projectDependencies[1].Name, new Version(2, 0, 0))
        });
        var expectedUpdateResult = new List<UpdateResult> { new(projectDependencies[1].Name, "1.0.0", "2.0.0") };
        _projectUpdater
            .HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)))
            .Returns(expectedUpdateResult);

        // Act
        await _target.DoUpdate();

        // Assert
        using (new AssertionScope())
        {
            _projectUpdater.Received(1).HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)));
            _repositoryProvider.Received(1).CommitChanges(_currentDir, _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
            await _repositoryProvider.Received(1).SubmitPullRequest(expectedUpdateResult,
                _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
        }
    }

    [Fact]
    public async Task Update_Should_Include()
    {
        // Arrange
        _config.Value.Projects[0].Include = new List<string>() { "Test1.*" };
        var projectList = new List<string>() { "TestProjectFile" };
        _projectUpdater
            .GetAllProjectFiles(_config.Value.Projects[0].Directories[0])
            .Returns(projectList);
        var projectDependencies =
            new List<DependencyDetails>()
            {
                new("TestDependency", new Version(1, 0, 0)), new("Test1.Dependency", new Version(1, 0, 0)),
            };
        _projectUpdater.ExtractAllPackages(projectList).Returns(projectDependencies);
        _projectUpdater.GetVersions(projectDependencies[0], _config.Value.Projects[0])
            .Returns(new List<DependencyDetails>
            {
                new(projectDependencies[0].Name, new Version(2, 0, 0)),
                new(projectDependencies[0].Name, new Version(1, 1, 0)),
                new(projectDependencies[0].Name, new Version(1, 0, 2)),
            });
        _projectUpdater.GetVersions(projectDependencies[1], _config.Value.Projects[0])
            .Returns(new List<DependencyDetails>
            {
                new(projectDependencies[1].Name, new Version(2, 0, 0)),
                new(projectDependencies[1].Name, new Version(1, 1, 0)),
                new(projectDependencies[1].Name, new Version(1, 0, 2)),
            });

        var expectedDependencyUpdate = new List<DependencyDetails>(new[]
        {
            new DependencyDetails(projectDependencies[1].Name, new Version(2, 0, 0))
        });
        var expectedUpdateResult = new List<UpdateResult> { new(projectDependencies[1].Name, "1.0.0", "2.0.0") };
        _projectUpdater
            .HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)))
            .Returns(expectedUpdateResult);

        // Act
        await _target.DoUpdate();

        // Assert
        using (new AssertionScope())
        {
            _projectUpdater.Received(1).HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)));
            _repositoryProvider.Received(1).CommitChanges(_currentDir, _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
            await _repositoryProvider.Received(1).SubmitPullRequest(expectedUpdateResult,
                _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
        }
    }

    [Fact]
    public async Task Update_Should_Exclude()
    {
        // Arrange
        _config.Value.Projects[0].Exclude = new List<string>() { "Test.*" };
        var projectList = new List<string>() { "TestProjectFile" };
        _projectUpdater
            .GetAllProjectFiles(_config.Value.Projects[0].Directories[0])
            .Returns(projectList);
        var projectDependencies =
            new List<DependencyDetails>()
            {
                new("TestDependency", new Version(1, 0, 0)), new("Test.Dependency", new Version(1, 0, 0)),
            };
        _projectUpdater.ExtractAllPackages(projectList).Returns(projectDependencies);
        _projectUpdater.GetVersions(projectDependencies[0], _config.Value.Projects[0])
            .Returns(new List<DependencyDetails>
            {
                new(projectDependencies[0].Name, new Version(2, 0, 0)),
                new(projectDependencies[0].Name, new Version(1, 1, 0)),
                new(projectDependencies[0].Name, new Version(1, 0, 2)),
            });
        _projectUpdater.GetVersions(projectDependencies[1], _config.Value.Projects[0])
            .Returns(new List<DependencyDetails>
            {
                new(projectDependencies[1].Name, new Version(2, 0, 0)),
                new(projectDependencies[1].Name, new Version(1, 1, 0)),
                new(projectDependencies[1].Name, new Version(1, 0, 2)),
            });

        var expectedDependencyUpdate = new List<DependencyDetails>(new[]
        {
            new DependencyDetails(projectDependencies[0].Name, new Version(2, 0, 0))
        });
        var expectedUpdateResult = new List<UpdateResult> { new(projectDependencies[0].Name, "1.0.0", "2.0.0") };
        _projectUpdater
            .HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)))
            .Returns(expectedUpdateResult);

        // Act
        await _target.DoUpdate();

        // Assert
        using (new AssertionScope())
        {
            _projectUpdater.Received(1).HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)));
            _repositoryProvider.Received(1).CommitChanges(_currentDir, _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
            await _repositoryProvider.Received(1).SubmitPullRequest(expectedUpdateResult,
                _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
        }
    }

    [Fact]
    public async Task Update_Should_GetPackageFromCacheIfExists()
    {
        // Arrange
        var projectList = new List<string>() { "TestProjectFile" };
        _projectUpdater
            .GetAllProjectFiles(_config.Value.Projects[0].Directories[0])
            .Returns(projectList);
        var projectDependencies =
            new List<DependencyDetails>() { new("TestDependency", new Version(1, 0, 0)), };
        _projectUpdater.ExtractAllPackages(projectList).Returns(projectDependencies);
        var expectedDependencyUpdate = new List<DependencyDetails>(new[]
        {
            new DependencyDetails(projectDependencies[0].Name, new Version(2, 0, 0))
        });
        _target.AddToCache(expectedDependencyUpdate[0], expectedDependencyUpdate);
        var expectedUpdateResult = new List<UpdateResult> { new(projectDependencies[0].Name, "1.0.0", "2.0.0") };
        _projectUpdater
            .HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)))
            .Returns(expectedUpdateResult);

        // Act
        await _target.DoUpdate();

        // Assert
        using (new AssertionScope())
        {
            _projectUpdater.Received(1).HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdate)));
            _repositoryProvider.Received(1).CommitChanges(_currentDir, _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
            await _repositoryProvider.Received(1).SubmitPullRequest(expectedUpdateResult,
                _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
            _memoryCache.Get(expectedDependencyUpdate[0].Name).Should().BeNull();
        }
    }

    [Fact]
    public async Task Update_Should_SkipPackageIfAlreadyProcessed()
    {
        // Arrange
        _config.Value.Projects[0].Groups = new List<string>() { "Test.*", "*" };
        var projectList = new List<string>() { "TestProjectFile" };
        _projectUpdater
            .GetAllProjectFiles(_config.Value.Projects[0].Directories[0])
            .Returns(projectList);
        var projectDependencies =
            new List<DependencyDetails>()
            {
                new("TestDependency", new Version(1, 0, 0)), new("Test.Dependency", new Version(1, 0, 0)),
            };
        _projectUpdater.ExtractAllPackages(projectList).Returns(projectDependencies);
        _projectUpdater.GetVersions(projectDependencies[0], _config.Value.Projects[0])
            .Returns(new List<DependencyDetails>
            {
                new(projectDependencies[0].Name, new Version(2, 0, 0)),
                new(projectDependencies[0].Name, new Version(1, 1, 0)),
                new(projectDependencies[0].Name, new Version(1, 0, 2)),
            });
        _projectUpdater.GetVersions(projectDependencies[1], _config.Value.Projects[0])
            .Returns(new List<DependencyDetails>
            {
                new(projectDependencies[1].Name, new Version(2, 0, 0)),
                new(projectDependencies[1].Name, new Version(1, 1, 0)),
                new(projectDependencies[1].Name, new Version(1, 0, 2)),
            });

        var expectedDependencyUpdateFirst = new List<DependencyDetails>(new[]
        {
            new DependencyDetails(projectDependencies[1].Name, new Version(2, 0, 0))
        });
        var expectedUpdateResultFirst = new List<UpdateResult> { new(projectDependencies[1].Name, "1.0.0", "2.0.0") };
        _projectUpdater
            .HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdateFirst)))
            .Returns(expectedUpdateResultFirst);
        var expectedDependencyUpdateSecond = new List<DependencyDetails>(new[]
        {
            new DependencyDetails(projectDependencies[0].Name, new Version(2, 0, 0))
        });
        var expectedUpdateResultSecond = new List<UpdateResult> { new(projectDependencies[0].Name, "1.0.0", "2.0.0") };
        _projectUpdater
            .HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdateSecond)))
            .Returns(expectedUpdateResultSecond);

        // Act
        await _target.DoUpdate();

        // Assert
        using (new AssertionScope())
        {
            _projectUpdater.Received(1).HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdateFirst)));
            _projectUpdater.Received(1).HandleProjectUpdate(_config.Value.Projects[0], projectList,
                Arg.Is<ICollection<DependencyDetails>>(detailsCollection =>
                    detailsCollection.SequenceEqual(expectedDependencyUpdateSecond)));
            _repositoryProvider.Received(1).CommitChanges(_currentDir, _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[0]);
            _repositoryProvider.Received(1).CommitChanges(_currentDir, _config.Value.Projects[0].Name,
                _config.Value.Projects[0].Groups[1]);
        }
    }
}