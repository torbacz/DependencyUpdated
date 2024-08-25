# DependencyUpdaterTool

The DependencyUpdaterTool is a command-line tool aimed to simplify the process of updating project dependencies. It currently supports .NET projects and is able to handle NuGet packages. In future updates, we plan to extend its functionality to other package platforms and languages.

## Key Features

- Updates project dependencies efficiently and automatically.
- Supports multiple package platforms, currently NuGet is supported and planned for future updates is extended support for other platforms.
- Has the ability to create Git commits and pull requests for changes. Currently, this feature is only available for Azure DevOps.

## Installation

The tool can be installed via .NET global tools. Run the following command in your terminal:

```powershell
dotnet tool install --global DependencyUpdaterTool
```

Please ensure that you have .NET 8 or later installed on your machine before running the installation command above.

## Usage
After installation, you can use the DependencyUpdaterTool in your terminal by running:

```powershell
dut
```

## Config
TODO

Note: Each config value can be overridden by environment variables. For example if you want to update

```json
{
  "UpdaterConfig": {
    "RepositoryType": ""
  }
}
```

Set UpdaterConfig__RepositoryType variable (all follows standard .NET naming conversion, check [docs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0) for more info)

## License
The DependencyUpdaterTool is available under the MIT license. For more information, see the LICENSE file in this repository.