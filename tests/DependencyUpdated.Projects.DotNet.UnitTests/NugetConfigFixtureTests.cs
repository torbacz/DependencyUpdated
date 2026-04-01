using System.Xml.Linq;
using DependencyUpdated.Core.Config;
using DependencyUpdated.Core.Interfaces;
using DependencyUpdated.Core.Models;
using DependencyUpdated.Core.Models.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace DependencyUpdated.Projects.DotNet.UnitTests;

public class NugetConfigFixtureTests
{
    private const string NugetConfigPath = "./Projects/nuget.config";
    private readonly IProjectUpdater _target;

    public NugetConfigFixtureTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.RegisterDotNetServices();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        _target = serviceProvider.GetRequiredKeyedService<IProjectUpdater>(ProjectType.DotNet);
    }

    [Fact]
    public void NugetConfigFixture_ShouldExist_WithLowercaseFilename()
    {
        // Assert that the file exists using its exact lowercase name (regression for rename from Nuget.config)
        File.Exists(NugetConfigPath).Should().BeTrue(
            "nuget.config must be named in all-lowercase so it is found on case-sensitive file systems");
    }

    [Fact]
    public void NugetConfigFixture_ShouldNotExist_WithOriginalMixedCaseFilename()
    {
        // Verifies the old Nuget.config (capital N) no longer exists as a separate file
        File.Exists("./Projects/Nuget.config").Should().BeFalse(
            "the old mixed-case Nuget.config must have been replaced by nuget.config");
    }

    [Fact]
    public void NugetConfigFixture_ShouldEndWithNewline()
    {
        // The PR explicitly added a trailing newline; verify that invariant is maintained
        var content = File.ReadAllText(NugetConfigPath);
        content.Should().EndWith("\n",
            "nuget.config must end with a newline character as introduced in this PR");
    }

    [Fact]
    public void NugetConfigFixture_ShouldBeValidXml()
    {
        // The fixture must parse as well-formed XML
        var content = File.ReadAllText(NugetConfigPath);
        var act = () => XDocument.Parse(content);
        act.Should().NotThrow("nuget.config must be valid XML");
    }

    [Fact]
    public void NugetConfigFixture_ShouldHaveConfigurationRoot()
    {
        var doc = XDocument.Load(NugetConfigPath);
        doc.Root!.Name.LocalName.Should().Be("configuration",
            "the root element of a NuGet config file must be <configuration>");
    }

    [Fact]
    public void NugetConfigFixture_ShouldContainPackageSourcesSection()
    {
        var doc = XDocument.Load(NugetConfigPath);
        var packageSources = doc.Root!.Element("packageSources");
        packageSources.Should().NotBeNull("nuget.config must declare a <packageSources> section");
    }

    [Fact]
    public void NugetConfigFixture_ShouldClearDefaultSources()
    {
        var doc = XDocument.Load(NugetConfigPath);
        var clear = doc.Root!.Element("packageSources")!.Element("clear");
        clear.Should().NotBeNull("nuget.config must contain <clear /> to remove any inherited package sources");
    }

    [Fact]
    public void NugetConfigFixture_ShouldDeclareNugetOrgSource()
    {
        var doc = XDocument.Load(NugetConfigPath);
        var add = doc.Root!
            .Element("packageSources")!
            .Elements("add")
            .SingleOrDefault(e => e.Attribute("key")?.Value == "nuget.org");

        add.Should().NotBeNull("nuget.config must register the nuget.org package source");
        add!.Attribute("value")!.Value.Should().Be(
            "https://api.nuget.org/v3/index.json",
            "the nuget.org source URL must point to the v3 index");
        add.Attribute("protocolVersion")!.Value.Should().Be("3",
            "the nuget.org source must use NuGet protocol version 3");
    }

    [Fact]
    public async Task GetVersions_Should_ResolvePackages_UsingLowercaseNugetConfigPath()
    {
        // Regression: after the rename the lowercase path must be used and must work end-to-end
        var packages = new DependencyDetails("Serilog", new Version(1, 0, 0));
        var projectConfiguration = new Project() { Type = ProjectType.DotNet };
        projectConfiguration.ApplyDefaultValue();
        projectConfiguration.DependencyConfigurations = projectConfiguration.DependencyConfigurations
            .Concat(new[] { NugetConfigPath }).ToArray();

        var result = await _target.GetVersions(packages, projectConfiguration);

        result.Should().NotBeNullOrEmpty(
            "GetVersions must successfully load package sources from the lowercase nuget.config path");
    }
}