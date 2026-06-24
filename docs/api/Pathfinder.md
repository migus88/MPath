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
| `RangeResult GetReachable(IAgent agent, Coordinate from, float budget)` | Finds every cell whose cheapest path cost from the origin is within the budget. |
| `bool HasLineOfSight(Coordinate from, Coordinate to)` | Returns whether an unobstructed straight line connects two cells. |
| `static int GetManhattanDistance(Coordinate from, Coordinate to)` | Manhattan (taxicab) distance `\|dx\| + \|dy\|`. |
| `static int GetChebyshevDistance(Coordinate from, Coordinate to)` | Chebyshev (chessboard) distance `max(\|dx\|, \|dy\|)`. |
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

### Movement range (reachability)

`GetReachable` returns every cell an agent can reach from an origin without exceeding a movement budget — useful for tactics/strategy games that highlight where a unit can move. It is a uniform-cost (Dijkstra) flood fill that honours the same movement rules as `GetPath` (diagonal movement, corner-cutting, occupied cells and agent clearance).

Per-step cost is `StraightMovementMultiplier` for cardinal moves and `DiagonalMovementMultiplier` for diagonal moves; when `IsCellWeightEnabled` is set, the destination cell's `Weight` is added to the step cost. The origin is always included with cost `0`.

```csharp
using var pathfinder = new Pathfinder(cells, 10, 10);
var agent = new SimpleAgent { Size = 1 };

// Every tile reachable within 4 movement points
using var range = pathfinder.GetReachable(agent, new Coordinate(4, 4), 4f);

foreach (var cell in range.Cells)
{
    Highlight(cell.Coordinate, cell.Cost);
}
```

See [RangeResult](RangeResult.md) and [ReachableCell](ReachableCell.md) for the returned types. Like `PathResult`, a `RangeResult` must be disposed (use `using`).

### Distance and line of sight

For lightweight spatial queries that don't need a full path, `Pathfinder` exposes grid metrics and a line-of-sight test:

- `GetManhattanDistance(from, to)` — the taxicab distance `|dx| + |dy|` (cardinal steps). This is the metric used by the A* heuristic.
- `GetChebyshevDistance(from, to)` — the chessboard distance `max(|dx|, |dy|)` (steps when diagonals cost the same as cardinals).
- `HasLineOfSight(from, to)` — whether a straight line between two cells is unobstructed.

The two distance methods are `static`, pure, and allocation-free — they only read the coordinates and ignore walls, weights and movement settings:

```csharp
var manhattan = Pathfinder.GetManhattanDistance(new Coordinate(1, 1), new Coordinate(4, 5)); // 7
var chebyshev = Pathfinder.GetChebyshevDistance(new Coordinate(1, 1), new Coordinate(4, 5)); // 4
```

`HasLineOfSight` traces a Bresenham line between the two cells and returns `false` if any cell **between** them is not walkable (or, when `IsCalculatingOccupiedCells` is enabled, is occupied). It is an O(distance) query that allocates nothing — handy for fog-of-war, ranged attacks or skipping pathfinding when a target is in plain sight.

```csharp
using var pathfinder = new Pathfinder(cells, 10, 10);

if (pathfinder.HasLineOfSight(new Coordinate(2, 2), new Coordinate(7, 5)))
{
    // Nothing blocks the shot.
}
```

The endpoints themselves are never tested for walkability, so a target standing on a blocked or occupied cell can still be "seen". Agent size is not considered — the check traces a single-cell ray. A cell always has line of sight to itself, and an out-of-range coordinate throws `ArgumentException`. 