# MPath

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/badge/Version-1.1.0-blue.svg)](src/mpath-unity-project/Packages/MPath/package.json)

A high-performance A* implementation for 2D grid navigation, designed primarily for game development (especially Unity) but fully compatible with any .NET project. MPath is optimized for speed and minimal memory allocations, making it ideal for real-time applications.

## Features

- Fast A* pathfinding with near-zero garbage collection overhead
- Allocates memory only when necessary to maximize performance
- Designed for 2D grid-based navigation in games
- First-class support for Unity with dedicated integration components
- Fully usable in any standalone .NET application
- Extensively tested with comprehensive unit tests
- Includes performance benchmarks

> ⚠️ **Note:** MPath is not yet thread-safe and should not be used across multiple threads.

## Benchmarks

| Method    | Mean      | Allocated   |
|---------- |----------:|------------:|
| MPath     |  5.092 ms |    24.06 KB |
| [AStarLite](https://github.com/valantonini/AStar) |  8.118 ms |  8.74 MB |
| [RoyTAStar](https://github.com/roy-t/AStar) | 59.028 ms | 12.29 MB |
| [LinqToAStar](https://arc.net/l/quote/iqcsmlgc) | 5,532.7 ms | 108.13 MB |

For detailed information about MPath's performance benchmarks, including implementation comparisons and path smoothing options, see the [benchmarks documentation](docs/benchmarks/README.md).

## Installation

<details>
<summary>Unity (via OpenUPM) - Recommended</summary>

### Option 1: Using OpenUPM CLI

1. Install the [OpenUPM CLI](https://openupm.com/docs/getting-started.html#installing-openupm-cli)
2. Run the following command in your Unity project folder:
   ```
   openupm add com.migsweb.mpath
   ```

### Option 2: Manual Installation via manifest.json

1. Open your Unity project's `Packages/manifest.json` file
2. Add the OpenUPM registry and the package to the file:
   ```json
   {
     "scopedRegistries": [
       {
         "name": "OpenUPM",
         "url": "https://package.openupm.com",
         "scopes": [
           "com.migsweb.mpath"
         ]
       }
     ],
     "dependencies": {
       "com.migsweb.mpath": "1.0.0",
       // ... other dependencies
     }
   }
   ```
3. Save the file and Unity will automatically download and install the package
</details>

<details>
<summary>Unity (via Git URL)</summary>

Add MPath to your project via the Unity Package Manager:

1. Open the Package Manager window in Unity (Window > Package Manager)
2. Click the "+" button and select "Add package from git URL..."
3. Enter the following URL:
   ```
   https://github.com/migus88/MPath.git?path=/src/mpath-unity-project/Packages/MPath
   ```

To use a specific version, append a tag with version (e.g `1.0.0`) to the URL:
   ```
   https://github.com/migus88/MPath.git?path=/src/mpath-unity-project/Packages/MPath#1.0.0
   ```
</details>

<details>
<summary>Unity (via .unitypackage)</summary>

1. Download the latest `.unitypackage` from the [Releases](https://github.com/migus88/MPath/releases) page
2. Import it into your Unity project (Assets > Import Package > Custom Package)
</details>

<details>
<summary>.NET Projects (via NuGet)</summary>

### Option 1: Using Package Manager Console (Visual Studio)

```powershell
Install-Package Migs.MPath
```

### Option 2: Using .NET CLI

```bash
dotnet add package Migs.MPath
```
</details>

## Quick Start

Here's a simple example of using MPath in a .NET project:

```csharp
// Create a 10x10 grid
var cells = new Cell[100];
for (int i = 0; i < 100; i++)
{
    int x = i % 10;
    int y = i / 10;
    cells[i] = new Cell
    {
        Coordinate = new Coordinate(x, y),
        IsWalkable = true,
        Weight = 1.0f
    };
}

// Add some obstacles
cells[12].IsWalkable = false;
cells[13].IsWalkable = false;

// Create a simple agent
var agent = new SimpleAgent { Size = 1 };

// Configure and create pathfinder
var settings = new PathfinderSettings { IsDiagonalMovementEnabled = true };
using var pathfinder = new Pathfinder(cells, 10, 10, settings);

// Find a path
var start = new Coordinate(1, 1);
var end = new Coordinate(8, 8);
using var result = pathfinder.GetPath(agent, start, end);

// Use the path
if (result.IsSuccess)
{
    Console.WriteLine($"Path found with {result.Length} steps!");
}
```

### Documentation

MPath comes with comprehensive documentation:

- [Getting Started Guide](docs/README.md) - Overview and basic usage
- [API Reference](docs/api/README.md) - Detailed class and interface documentation
- [Integration Guides](docs/guides/) - Specific guides for Unity and .NET projects

The API reference provides detailed information about all public classes, interfaces, and methods, with examples for each component.

### Important Notes

- Always dispose `PathResult` objects after use (use `using` statements)
- Reuse the pathfinder instance for best performance

## License

MPath is licensed under the MIT License. See [LICENSE](LICENSE) for details.