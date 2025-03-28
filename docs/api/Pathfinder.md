# Pathfinder Class

**Namespace:** `Migs.MPath.Core`

A high-performance A* pathfinding implementation for grid-based environments.

## Constructors

| Constructor | Description |
|-------------|-------------|
| `Pathfinder(Cell[] cells, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)` | Initializes with a pre-existing cell array. |
| `Pathfinder(ICellHolder[] holders, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)` | Initializes with cell holders. |
| `Pathfinder(Cell[,] cellsMatrix, IPathfinderSettings settings = null)` | Initializes with a cell matrix. |
| `Pathfinder(ICellHolder[,] cellHoldersMatrix, IPathfinderSettings settings = null)` | Initializes with a cell holder matrix. |

## Methods

| Method | Description |
|--------|-------------|
| `PathResult GetPath(IAgent agent, Coordinate from, Coordinate to)` | Calculates a path from starting position to destination. |
| `Pathfinder EnablePathCaching(IPathCaching pathCachingHandler = null)` | Enables path caching with optional custom implementation. |
| `Pathfinder DisablePathCaching()` | Disables path caching. |
| `Pathfinder InvalidateCache()` | Clears the current path cache without disabling caching. |
| `void Dispose()` | Releases resources used by the Pathfinder. |

## Remarks

- Implements `IDisposable` and should be disposed when no longer needed.
- Uses unsafe code and array pooling internally to minimize garbage collection.
- The pathfinding algorithm is optimized for speed and memory efficiency.
- For larger grids, reuse the same `Pathfinder` instance for multiple path calculations.
- Path caching is disabled by default but can be enabled for improved performance when the same paths are requested multiple times.

## Examples

```csharp
// Create a grid and pathfinder
var cells = new Cell[100]; 
// Initialize cells...

using var pathfinder = new Pathfinder(cells, 10, 10);

// Optional: Enable path caching for better performance with repeated paths
pathfinder.EnablePathCaching();

var agent = new SimpleAgent { Size = 1 };
using var result = pathfinder.GetPath(agent, new Coordinate(1, 1), new Coordinate(8, 8));

// Clear the cache if the environment changes
pathfinder.InvalidateCache();

// Disable caching when no longer needed
pathfinder.DisablePathCaching();
``` 