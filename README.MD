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
The most recent config can be found here:
`https://github.com/torbacz/DependencyUpdated/blob/main/src/DependencyUpdated/config.json`

Note: Each config value can be overridden by environment variables. For example if you want to update

```json
{
  "UpdaterConfig": {
    "RepositoryType": ""
  }
}
```

Set UpdaterConfig__RepositoryType variable (all follows standard .NET naming conversion, check [docs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-8.0) for more info)

## Local development
When working on local machine, there are several things that needs to be setup:
- You need to provide configuration for your use case. You can either set each variable as environment variable or fill `config.json`. Make sure not to commit `config.json` file into repository.
- Before executing program, make sure to set up working directory to repository you want to test against.
    - Keep in mind that you can override default config path with "--configPath "pathToConfig\config.json" programm argument.
- All paths are resolved relative to working directory

## License
The DependencyUpdaterTool is available under the MIT license. For more information, see the LICENSE file in this repository.