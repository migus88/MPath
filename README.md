# MPath

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](https://opensource.org/licenses/MIT)
[![Version](https://img.shields.io/badge/Version-2.0.0-blue.svg)](src/mpath-unity-project/Packages/MPath/package.json)

A high-performance A* implementation for 2D grid navigation, designed primarily for game development (especially Unity) but fully compatible with any .NET project. MPath is optimized for speed and minimal memory allocations, making it ideal for real-time applications.

## Features

- Fast A* pathfinding with near-zero garbage collection overhead
- Allocates memory only when necessary to maximize performance
- Designed for 2D grid-based navigation in games
- Movement-range queries (`GetReachable`) for "where can this unit move?" mechanics
- Configurable corner-cutting (`CornerBlocking`): walls and occupied cells can block diagonal steps separately
- Deterministic tie-breaking (`TieBreaking`): among equal-cost routes, the one with fewer diagonals or fewer turns, with exact integer costs; results report integer `Steps` / `DiagonalSteps`
- Per-query options (`QueryOptions`): units block or not, and specific occupied cells can be entered for one query, without touching the grid
- Single-cell updates (`SetWalkable`, `SetOccupied`, `SetWeight`, `SetCell`): O(1) writes into your grid that keep the path cache valid
- Short queries stay short on large grids: search state lives in the pathfinder and is reset lazily, and `GridSnapshotMode.Manual` stops `PathCell[,]`/holder grids from being copied per query
- Flat `PathCell[]` grids, column- or row-major (`CellLayout`): a cell's slot is its position, 8 bytes a cell, searched in place and never reordered
- Distance metrics (Manhattan, Chebyshev) and line-of-sight checks for range and visibility logic
- Stepwise search (`BeginStepwiseSearch`) for visualizing how the algorithm explores the grid
- First-class support for Unity with dedicated integration components
- Fully usable in any standalone .NET application
- Extensively tested with comprehensive unit tests
- Includes performance benchmarks

> ⚠️ **Note:** MPath is not yet thread-safe and should not be used across multiple threads.

## Benchmarks

| Method    | Mean      | Allocated   |
|---------- |----------:|------------:|
| MPath     |  4.223 ms |       178 B |
| [AStarLite](https://github.com/valantonini/AStar) |  8.042 ms |  8.75 MB |
| [RoyTAStar](https://github.com/roy-t/AStar) | 62.412 ms | 12.30 MB |
| [LinqToAStar](https://arc.net/l/quote/iqcsmlgc) | 5,884.7 ms | 108.13 MB |

Compared with 1.2.1 on the same machine, 2.0 finds the maze path about 21% faster. On grids with 20% random walls it is 6–7x faster. Short queries and small movement ranges are 25–90x faster, and up to 25,000x with `GridSnapshotMode.Manual`. A query still allocates only its result object, a few dozen bytes.

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
// Create matrix of Cells
var cells = new PathCell[10, 10];

// Or do it with gameObjects that implements ICellHolder
[SerializeField] private FieldCell[] _cells;

// Create a pathfinder
_pathfinder = new Pathfinder(cells);

// Optionally pass a configuration file (see docs)
_pathfinder = new Pathfinder(cells, config);

// You can also enable path caching
_pathfinder.EnablePathCaching();

// Find a path
var start = new Coordinate(1, 1);
var end = new Coordinate(8, 8);

using var result = pathfinder.GetPath(start, end);

// Larger units: pass their size (always the third argument)
using var tankPath = pathfinder.GetPath(start, end, 2);

// Use the path
if (result.IsSuccess)
{
    Debug.Log($"Path found with {result.Length} steps!");
}
```

### Movement range

Need to know every tile a unit can reach within a movement budget (e.g. for a tactics game)? Use `GetReachable`:

```csharp
// Every cell whose cheapest path cost is <= 500 (5 straight steps at the default step costs 100/141)
using var range = pathfinder.GetReachable(new Coordinate(4, 4), 500);

foreach (var cell in range)
{
    Debug.Log($"{cell.Coordinate} reachable for {cell.Cost}");
}
```

Units block movement but not a threat range? Need the mover's own cell or an attack target to be enterable? Pass per-query `QueryOptions` (allocation-free; the grid is left untouched):

```csharp
using var threat = pathfinder.GetReachable(new Coordinate(4, 4), 500, new QueryOptions(OccupancyRule.Ignore));
using var attack = pathfinder.GetPath(from, target, new QueryOptions(isDestinationAlwaysEnterable: true));
```

### Distance and line of sight

For range checks and visibility — without computing a full path — use the distance metrics and the line-of-sight test:

```csharp
// Pure grid distances (static, allocation-free)
int manhattan = Pathfinder.GetManhattanDistance(start, end); // |dx| + |dy|
int chebyshev = Pathfinder.GetChebyshevDistance(start, end);  // max(|dx|, |dy|)

// Is the target visible (no walls in between)?
if (pathfinder.HasLineOfSight(shooter, target))
{
    Debug.Log("Clear shot!");
}

// Or see through terrain that blocks movement but not vision (water, pits, glass):
pathfinder.HasLineOfSight(shooter, target, LineOfSightMode.IgnoreUnwalkableCells);
```

See the [Distance and Line of Sight guide](docs/guides/distance-and-line-of-sight.md) for details.

### Visualizing the search

Want to *show* how A* explores the grid (for teaching or a demo)? `BeginStepwiseSearch` runs the same search as `GetPath`, but one cell at a time, exposing the frontier and explored area after every step:

```csharp
using var search = pathfinder.BeginStepwiseSearch(new Coordinate(1, 1), new Coordinate(8, 8));

while (!search.IsComplete)
{
    var step = search.Tick();
    Render(step.Searched); // open (frontier) vs closed (expanded) cells, with A* scores
}

if (search.Tick().State == SearchState.Success)
{
    DrawPath(search.Tick().Path);
}
```

This is an educational/visualization tool — `GetPath` remains the way to compute paths in production. See the [Visualizing the Search guide](docs/guides/visualizing-search.md) for details.

### Documentation

MPath comes with comprehensive documentation:

- [Getting Started Guide](docs/README.md) - Overview and basic usage
- [API Reference](docs/api/README.md) - Detailed class and interface documentation
- [Upgrading](docs/upgrading.md) - What changed between versions and how to migrate, one section per release
- [Integration Guides](docs/guides/) - Specific guides for Unity and .NET projects
- [AI Agent & LLM Reference](docs/llms.md) - A single, machine-first page so AI coding agents can use MPath correctly without reading the whole docs tree

The API reference provides detailed information about all public classes, interfaces, and methods, with examples for each component.

### Important Notes

- Always dispose `PathResult`, `RangeResult`, and `StepwiseSearch` objects after use (use `using` statements)
- Reuse the pathfinder instance for best performance

## License

MPath is licensed under the MIT License. See [LICENSE](LICENSE) for details.