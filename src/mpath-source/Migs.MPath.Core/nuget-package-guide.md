# NuGet Package Guide for MPath

This guide explains how to build and publish the MPath NuGet package.

## Prerequisites

- .NET SDK (version 6.0 or higher recommended)
- NuGet CLI or .NET CLI (included with the SDK)
- NuGet.org account (if you plan to publish to nuget.org)

## Building the NuGet Package

The project is already configured to generate a NuGet package on build using the `GeneratePackageOnBuild` property in the .csproj file.

1. Navigate to the Migs.MPath.Core project directory:

```bash
cd /path/to/mpath-source/Migs.MPath.Core
```

2. Build the project in Release configuration:

```bash
dotnet build --configuration Release
```

3. The NuGet package will be generated in the `bin/Release` folder with the name `Migs.MPath.1.0.0.nupkg` (version number may vary).

## Examining the Package

You can inspect the contents of your NuGet package using the NuGet Package Explorer or by extracting it (it's a zip file):

```bash
# See what's inside the package using NuGet CLI
nuget spec bin/Release/Migs.MPath.1.0.0.nupkg
```

## Publishing to NuGet.org

1. Create an account on [NuGet.org](https://www.nuget.org/) if you don't have one yet.

2. Generate an API key from your NuGet account:
   - Go to your account settings
   - Select "API Keys"
   - Create a new API key with appropriate scope

3. Push the package to NuGet.org:

```bash
dotnet nuget push bin/Release/Migs.MPath.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
```

## Publishing to GitHub Packages (Alternative)

You can also publish your package to GitHub Packages:

1. Create a GitHub Personal Access Token with `read:packages`, `write:packages`, and `delete:packages` scopes.

2. Add GitHub Packages as a source in your NuGet configuration:

```bash
dotnet nuget add source https://nuget.pkg.github.com/migus88/index.json --name github --username migus88 --password YOUR_GITHUB_TOKEN
```

3. Push the package to GitHub Packages:

```bash
dotnet nuget push bin/Release/Migs.MPath.1.0.0.nupkg --source github
```

## Versioning

When updating your package, remember to increment the version number in the .csproj file:

```xml
<PropertyGroup>
  <PackageId>Migs.MPath</PackageId>
  <Version>1.0.0</Version>
  <AssemblyVersion>1.0.0</AssemblyVersion>
</PropertyGroup>
```

Use [Semantic Versioning](https://semver.org/) for your package:
- MAJOR version for incompatible API changes
- MINOR version for backward-compatible functionality additions
- PATCH version for backward-compatible bug fixes

## Setting Up CI/CD for Package Publishing

You can automate the package publishing process using GitHub Actions. Here's a basic workflow:

```yaml
name: Release NuGet Package

on:
  release:
    types: [published]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v2
    - name: Setup .NET
      uses: actions/setup-dotnet@v1
      with:
        dotnet-version: 6.0.x
    - name: Build and Pack
      run: dotnet build src/mpath-source/Migs.MPath.Core/Migs.MPath.Core.csproj --configuration Release
    - name: Publish to NuGet
      run: dotnet nuget push src/mpath-source/Migs.MPath.Core/bin/Release/Migs.MPath.*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

## Consuming the Package

Once published, users can install your package using:

```bash
dotnet add package Migs.MPath
```

Or via the NuGet Package Manager in Visual Studio. 