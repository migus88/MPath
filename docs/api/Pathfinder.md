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
| `PathResult GetPath(IAgent agent, Coordinate from, Coordinate to, QueryOptions options)` | `GetPath` with per-query [`QueryOptions`](QueryOptions.md) (occupancy override, always-enterable cells). |
| `RangeResult GetReachable(IAgent agent, Coordinate from, float budget)` | Finds every cell whose cheapest path cost from the origin is within the budget. |
| `RangeResult GetReachable(IAgent agent, Coordinate from, float budget, QueryOptions options)` | `GetReachable` with per-query [`QueryOptions`](QueryOptions.md). |
| `StepwiseSearch BeginStepwiseSearch(IAgent agent, Coordinate from, Coordinate to)` | Begins a tick-by-tick A* search for visualizing the algorithm's progress. |
| `StepwiseSearch BeginStepwiseSearch(IAgent agent, Coordinate from, Coordinate to, QueryOptions options)` | `BeginStepwiseSearch` with per-query [`QueryOptions`](QueryOptions.md), applied on every tick. |
| `bool HasLineOfSight(Coordinate from, Coordinate to, LineOfSightMode mode = BlockedByUnwalkableCells)` | Returns whether an unobstructed straight line connects two cells. |
| `static int GetManhattanDistance(Coordinate from, Coordinate to)` | Manhattan (taxicab) distance `\|dx\| + \|dy\|`. |
| `static int GetChebyshevDistance(Coordinate from, Coordinate to)` | Chebyshev (chessboard) distance `max(\|dx\|, \|dy\|)`. |
| `Cell GetCell(Coordinate coordinate)` | Returns the current cell at a coordinate, read from your grid (array, matrix or holder). |
| `void SetWalkable(Coordinate coordinate, bool isWalkable)` | Updates one cell's walkability in your `Cell[]`/`Cell[,]`. See [cell updates](#cell-updates). |
| `void SetOccupied(Coordinate coordinate, bool isOccupied)` | Updates one cell's occupancy. See [cell updates](#cell-updates). |
| `void SetWeight(Coordinate coordinate, float weight)` | Updates one cell's weight. See [cell updates](#cell-updates). |
| `void SetCell(Coordinate coordinate, in Cell cell)` | Updates walkability, occupancy and weight at once; `cell.Coordinate` is ignored. |
| `int GetIndex(Coordinate coordinate)` | The cell's index in the layout queries use: `X * fieldHeight + Y` (column-major). |
| `Pathfinder EnablePathCaching(IPathCaching pathCachingHandler = null)` | Enables path caching with optional custom implementation. |
| `Pathfinder DisablePathCaching()` | Disables path caching. |
| `Pathfinder InvalidateCache()` | Clears the current path cache without disabling caching. |
| `void Dispose()` | Returns the pooled cell buffer, disposes the path cache and ends an active `StepwiseSearch`. Idempotent. |

## Remarks

- Implements `IDisposable` and should be disposed when no longer needed. The `Cell[,]`, `ICellHolder[]` and `ICellHolder[,]` constructors rent their cell buffer from `ArrayPool<Cell>.Shared` and `Dispose` gives it back (skipped, the buffer is never reused and is left to the GC). The `Cell[]` constructor uses your array directly and owns no pooled memory, so skipping `Dispose` is harmless there unless path caching is enabled (the cache holds pooled path results).
- `Dispose` is idempotent: the buffer goes back to the pool exactly once. It ends an active `StepwiseSearch` first (that session's later `Tick()` throws `ObjectDisposedException`). After `Dispose`, every instance method (`GetPath`, `GetReachable`, `HasLineOfSight`, `BeginStepwiseSearch`, `EnablePathCaching`, `DisablePathCaching`, `InvalidateCache`, `GetCell`, `GetIndex`, the `Set*` cell updates) throws `ObjectDisposedException`; the static distance helpers keep working.
- Uses unsafe code and array pooling internally to minimize garbage collection.
- The pathfinding algorithm is optimized for speed and memory efficiency.
- For larger grids, reuse the same `Pathfinder` instance for multiple path calculations.
- `GetPath` and `BeginStepwiseSearch` throw `ArgumentException` when `from` or `to` is outside the grid.
- A destination the agent cannot enter (not walkable, occupied while `IsCalculatingOccupiedCells` is enabled, or without clearance for the agent's size) fails immediately without searching: `GetPath` returns a failure result and a `StepwiseSearch` starts complete in the `Failure` state. A query from a cell to itself always succeeds with an empty path.
- Settings are frozen at construction. To vary occupancy per query (units block for movement, not for a threat range) or to enter specific occupied cells (the mover's own cells, an attack target), pass [`QueryOptions`](QueryOptions.md) to the overloads that take it. `default(QueryOptions)` behaves exactly like the overloads without options, and the options never change the grid.
- Path caching is disabled by default but can be enabled for improved performance when the same paths are requested multiple times.

## Cell updates

`SetWalkable`, `SetOccupied`, `SetWeight` and `SetCell` change one cell and keep the bookkeeping in the pathfinder:

- **`Cell[]` and `Cell[,]` constructors:** the write goes to your own array or matrix (the pathfinder keeps the reference), so your grid and the pathfinder stay in sync and the next query sees the change. O(1) and allocation-free while the grid is in layout (`cells[GetIndex(c)]` holds `c`, which every query establishes by sorting a `Cell[]` into that order; a `Cell[,]` holds `c` at `[c.X, c.Y]`). Otherwise the cell is found by a linear search and your array is never reordered by a setter.
- **`ICellHolder[]` and `ICellHolder[,]` constructors:** the setters throw `NotSupportedException`. The holders are the source of truth and are re-read on every query, so update the holder itself (and call `InvalidateCache()` when caching is on). `GetCell` and `GetIndex` work in every mode.
- **Path cache:** a setter that changes a value cached paths depend on clears the cache (walkability always; occupancy only with `IsCalculatingOccupiedCells`; weight only with `IsCellWeightEnabled`). Setting the current value again leaves the cache alone. Direct writes to your grid still need `InvalidateCache()`.
- Out-of-grid coordinates throw `ArgumentException`; a grid with no cell carrying the coordinate throws `InvalidOperationException`.

```csharp
pathfinder.SetOccupied(unit.Position, false);
pathfinder.SetOccupied(destination, true);
pathfinder.SetWalkable(new Coordinate(4, 2), false); // a wall appears
```

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

// Cell updates through the pathfinder clear the cache themselves
pathfinder.SetWalkable(new Coordinate(4, 4), false);

// Clear the cache by hand after writing to your grid directly
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

### Visualizing the search (stepwise)

`BeginStepwiseSearch` returns a [`StepwiseSearch`](StepwiseSearch.md) that runs the **same** A* expansion as `GetPath`, but one cell at a time, so you can display how the algorithm explores the grid. It is an **educational / visualization** facility — `GetPath` remains the way to compute a path in production.

Each `Tick()` expands a single cell and returns a [`SearchStep`](SearchStep.md) snapshot containing the accumulated searched area (every discovered cell, tagged [`Open`](SearchNode.md) or `Closed`, with its A* scores) and — once the destination is reached — the resulting path.

```csharp
using var pathfinder = new Pathfinder(cells, 10, 10);
var agent = new SimpleAgent { Size = 1 };

using var search = pathfinder.BeginStepwiseSearch(agent, new Coordinate(1, 1), new Coordinate(8, 8));

while (!search.IsComplete)
{
    var step = search.Tick();
    Render(step.Searched);          // highlight open (frontier) vs closed (expanded) cells
}

var result = search.Tick();         // idempotent once complete
if (result.State == SearchState.Success)
{
    DrawPath(result.Path);          // raw A* path (origin excluded, like PathResult)
}
```

The session **pins** the pathfinder's cell buffer for the lifetime of the search, so it must be disposed (use `using`), and the owning `Pathfinder` must not be used for any other query until then. See the [Visualizing the search](../guides/visualizing-search.md) guide for a full walk-through.

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

`HasLineOfSight` traces a Bresenham line between the two cells and returns `false` if any cell **between** them blocks sight. It is an O(distance) query that allocates nothing — handy for fog-of-war, ranged attacks or skipping pathfinding when a target is in plain sight.

```csharp
using var pathfinder = new Pathfinder(cells, 10, 10);

if (pathfinder.HasLineOfSight(new Coordinate(2, 2), new Coordinate(7, 5)))
{
    // Nothing blocks the shot.
}
```

By default (`LineOfSightMode.BlockedByUnwalkableCells`) a non-walkable cell blocks sight. Pass [`LineOfSightMode.IgnoreUnwalkableCells`](LineOfSightMode.md) to see through obstacles that block movement but not vision (water, a pit, a glass wall):

```csharp
// Terrain doesn't block the shot, but units (occupied cells) still might.
var visible = pathfinder.HasLineOfSight(shooter, target, LineOfSightMode.IgnoreUnwalkableCells);
```

The mode only governs walkability. Independently, an occupied cell blocks sight when `IsCalculatingOccupiedCells` is enabled — so "ignore terrain, but units still block" is expressible. The endpoints themselves are never tested, so a target standing on a blocked or occupied cell can still be "seen". Agent size is not considered — the check traces a single-cell ray. A cell always has line of sight to itself, and an out-of-range coordinate throws `ArgumentException`. 