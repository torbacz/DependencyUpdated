using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Core.Models.Enums;
using DependencyUpdated.Core.Strategies;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace DependencyUpdated.Core.UnitTests;

public class UpdateStrategyTests
{
    [Fact]
    public async Task CombinedStrategy_Should_PushOnceWithAllDirectoryUpdates()
    {
        var project = CreateProject(["FirstDir", "SecondDir"]);
        var projectUpdater = Substitute.For<IProjectUpdater>();
        var repositoryProvider = Substitute.For<IRepositoryProvider>();
        var processor = CreateProcessor(projectUpdater, repositoryProvider);
        var firstFiles = new List<string> { "FirstProject" };
        var secondFiles = new List<string> { "SecondProject" };
        var firstDependency = new DependencyDetails("FirstDependency", new Version(1, 0, 0));
        var secondDependency = new DependencyDetails("SecondDependency", new Version(1, 0, 0));
        var firstUpdate = new UpdateResult(firstDependency.Name, "1.0.0", "2.0.0");
        var secondUpdate = new UpdateResult(secondDependency.Name, "1.0.0", "2.0.0");

        projectUpdater.GetAllProjectFiles("FirstDir").Returns(firstFiles);
        projectUpdater.GetAllProjectFiles("SecondDir").Returns(secondFiles);
        projectUpdater.ExtractAllPackages(firstFiles).Returns([firstDependency]);
        projectUpdater.ExtractAllPackages(secondFiles).Returns([secondDependency]);
        projectUpdater.GetVersions(firstDependency, project)
            .Returns([firstDependency with { Version = new Version(2, 0, 0) }]);
        projectUpdater.GetVersions(secondDependency, project)
            .Returns([secondDependency with { Version = new Version(2, 0, 0) }]);
        projectUpdater.HandleProjectUpdate(project, firstFiles, Arg.Any<ICollection<DependencyDetails>>())
            .Returns([firstUpdate]);
        projectUpdater.HandleProjectUpdate(project, secondFiles, Arg.Any<ICollection<DependencyDetails>>())
            .Returns([secondUpdate]);
        repositoryProvider.CommitChanges(Arg.Any<string>(), project.Name, "*").Returns(true);

        var strategy = new CombinedProjectUpdateStrategy(projectUpdater, repositoryProvider, processor);
        await strategy.Update(project, Environment.CurrentDirectory);

        repositoryProvider.Received(2).CommitChanges(Environment.CurrentDirectory, project.Name, "*");
        repositoryProvider.Received(1).PushChanges(Environment.CurrentDirectory, project.Name, "*");
        await repositoryProvider.Received(1).SubmitPullRequest(
            Arg.Is<IReadOnlyCollection<UpdateResult>>(updates => updates.SequenceEqual(
                new List<UpdateResult> { firstUpdate, secondUpdate })),
            project.Name,
            "*");
        repositoryProvider.Received(1).CleanAndSwitchToDefaultBranch(Environment.CurrentDirectory);
    }

    [Fact]
    public async Task SeparateStrategy_Should_PushEachDirectoryIndependently()
    {
        var project = CreateProject(["FirstDir", "SecondDir"]);
        project.EachDirectoryAsSeparate = true;
        project.Name = null!;
        var projectUpdater = Substitute.For<IProjectUpdater>();
        var repositoryProvider = Substitute.For<IRepositoryProvider>();
        var processor = CreateProcessor(projectUpdater, repositoryProvider);
        var firstFiles = new List<string> { "FirstProject" };
        var secondFiles = new List<string> { "SecondProject" };
        var firstDependency = new DependencyDetails("FirstDependency", new Version(1, 0, 0));
        var secondDependency = new DependencyDetails("SecondDependency", new Version(1, 0, 0));

        projectUpdater.GetAllProjectFiles("FirstDir").Returns(firstFiles);
        projectUpdater.GetAllProjectFiles("SecondDir").Returns(secondFiles);
        projectUpdater.ExtractAllPackages(firstFiles).Returns([firstDependency]);
        projectUpdater.ExtractAllPackages(secondFiles).Returns([secondDependency]);
        projectUpdater.GetVersions(Arg.Any<DependencyDetails>(), project)
            .Returns(callInfo =>
            {
                var dependency = callInfo.Arg<DependencyDetails>();
                return new[] { dependency with { Version = new Version(2, 0, 0) } };
            });
        projectUpdater.HandleProjectUpdate(Arg.Any<Project>(), Arg.Any<IReadOnlyCollection<string>>(),
                Arg.Any<ICollection<DependencyDetails>>())
            .Returns([new UpdateResult("Dependency", "1.0.0", "2.0.0")]);
        repositoryProvider.CommitChanges(Arg.Any<string>(), Arg.Any<string>(), "*").Returns(true);

        var strategy = new SeparateDirectoryUpdateStrategy(projectUpdater, repositoryProvider, processor);
        await strategy.Update(project, Environment.CurrentDirectory);

        repositoryProvider.Received(2).CommitChanges(Environment.CurrentDirectory, Arg.Any<string>(), "*");
        repositoryProvider.Received(2).PushChanges(Environment.CurrentDirectory, Arg.Any<string>(), "*");
        await repositoryProvider.Received(2).SubmitPullRequest(
            Arg.Any<IReadOnlyCollection<UpdateResult>>(), Arg.Any<string>(), "*");
        repositoryProvider.Received(2).CleanAndSwitchToDefaultBranch(Environment.CurrentDirectory);
    }

    private static Project CreateProject(IReadOnlyList<string> directories) => new()
    {
        Name = "TestProject",
        Type = ProjectType.DotNet,
        Version = VersionUpdateType.Major,
        Directories = directories,
        Groups = ["*"]
    };

    private static DependencyUpdateProcessor CreateProcessor(
        IProjectUpdater projectUpdater,
        IRepositoryProvider repositoryProvider)
    {
        return new DependencyUpdateProcessor(
            projectUpdater,
            repositoryProvider,
            Substitute.For<ILogger>(),
            new MemoryCache(new MemoryCacheOptions()));
    }
}
