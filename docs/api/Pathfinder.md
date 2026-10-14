# Pathfinder Class

**Namespace:** `Migs.MPath.Core`

A high-performance A* pathfinding implementation for grid-based environments.

## Constructors

| Constructor | Description |
|-------------|-------------|
| `Pathfinder(Cell[] cells, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)` | Initializes over your cell array, laid out as [`CellLayout`](CellLayout.md) says (default `ColumnMajor`): a cell's index is its position. Keeps a reference and never reorders or writes it during queries. Throws `ArgumentException` when the array is shorter than `fieldWidth * fieldHeight`. |
| `Pathfinder(ICellHolder[] holders, int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)` | Initializes with cell holders, laid out as [`CellLayout`](CellLayout.md) says; a `null` holder is a wall. Throws `ArgumentException` when the array is shorter than `fieldWidth * fieldHeight`. |
| `Pathfinder(Cell[,] cellsMatrix, IPathfinderSettings settings = null)` | Initializes with a cell matrix: the cell at (X, Y) is `[X, Y]`. |
| `Pathfinder(ICellHolder[,] cellHoldersMatrix, IPathfinderSettings settings = null)` | Initializes with a cell holder matrix: the cell at (X, Y) is held by `[X, Y]`; a `null` holder is a wall. |

Every constructor throws `ArgumentOutOfRangeException` when `CellLayout` is neither `ColumnMajor` nor `RowMajor`, when `StraightStepCost` or `DiagonalStepCost` is below 1, or when `(width + height)` times the larger step cost in use (the diagonal one only with diagonal movement) exceeds 1,073,741,823 (`int.MaxValue / 2`), the headroom that keeps integer route costs from overflowing.

## Methods

| Method | Description |
|--------|-------------|
| `PathResult GetPath(Coordinate from, Coordinate to, int agentSize = 1)` | Calculates a cheapest path for an `agentSize` x `agentSize` agent (single cell by default). See [agent size](#agent-size). |
| `PathResult GetPath(Coordinate from, Coordinate to, int agentSize, QueryOptions options)` | `GetPath` with per-query [`QueryOptions`](QueryOptions.md) (occupancy override, always-enterable cells). |
| `RangeResult GetReachable(Coordinate from, int budget, int agentSize = 1)` | Finds every cell whose cheapest path cost from the origin is within the budget. |
| `RangeResult GetReachable(Coordinate from, int budget, int agentSize, QueryOptions options)` | `GetReachable` with per-query [`QueryOptions`](QueryOptions.md). |
| `StepwiseSearch BeginStepwiseSearch(Coordinate from, Coordinate to, int agentSize = 1)` | Begins a tick-by-tick A* search for visualizing the algorithm's progress. |
| `StepwiseSearch BeginStepwiseSearch(Coordinate from, Coordinate to, int agentSize, QueryOptions options)` | `BeginStepwiseSearch` with per-query [`QueryOptions`](QueryOptions.md), applied on every tick. |
| `bool HasLineOfSight(Coordinate from, Coordinate to, LineOfSightMode mode = BlockedByUnwalkableCells)` | Returns whether an unobstructed straight line connects two cells. |
| `static int GetManhattanDistance(Coordinate from, Coordinate to)` | Manhattan (taxicab) distance `\|dx\| + \|dy\|`. |
| `static int GetChebyshevDistance(Coordinate from, Coordinate to)` | Chebyshev (chessboard) distance `max(\|dx\|, \|dy\|)`. |
| `Cell GetCell(Coordinate coordinate)` | Returns the current cell at a coordinate, read from your grid (array, matrix or holder; a `null` holder reads as `default`, a wall). |
| `void SetWalkable(Coordinate coordinate, bool isWalkable)` | Updates one cell's walkability in your `Cell[]`/`Cell[,]`. See [cell updates](#cell-updates). |
| `void SetOccupied(Coordinate coordinate, bool isOccupied)` | Updates one cell's occupancy. See [cell updates](#cell-updates). |
| `void SetWeight(Coordinate coordinate, int weight)` | Updates one cell's weight. See [cell updates](#cell-updates). |
| `void SetCell(Coordinate coordinate, in Cell cell)` | Updates walkability, occupancy and weight at once. |
| `int GetIndex(Coordinate coordinate)` | `Cell[]` and `ICellHolder[]`: the cell's index in your array by its [`CellLayout`](CellLayout.md) (`X * fieldHeight + Y` or `Y * fieldWidth + X`). Matrices: `X * fieldHeight + Y`. Always the index `RangeResult.CopyCostsTo` writes the cell's cost at. O(1). |
| `Pathfinder EnablePathCaching(IPathCaching pathCachingHandler = null)` | Enables path caching with optional custom implementation. |
| `Pathfinder DisablePathCaching()` | Disables path caching. |
| `Pathfinder InvalidateCache()` | Clears the current path cache without disabling caching. `Refresh()` does this too. |
| `Pathfinder Refresh()` | Says your grid changed outside the `Set*` methods: the next query copies the snapshot again (`Cell[,]`, holders), and the path cache is cleared now. Needed after direct edits with [`GridSnapshotMode.Manual`](GridSnapshotMode.md) or with caching on. |
| `void Dispose()` | Returns the pooled buffers (cells, search state, open set), disposes the path cache and ends an active `StepwiseSearch`. Idempotent. |

## Remarks

- Implements `IDisposable` and should be disposed when no longer needed. The `Cell[,]`, `ICellHolder[]` and `ICellHolder[,]` constructors rent their cell buffer from `ArrayPool<Cell>.Shared` and `Dispose` gives it back (skipped, the buffer is never reused and is left to the GC). The `Cell[]` constructor searches your array directly and rents no cell buffer. Skipping `Dispose` costs that memory going to the GC instead of the pool.
- `Dispose` is idempotent: the buffer goes back to the pool exactly once. It ends an active `StepwiseSearch` first (that session's later `Tick()` throws `ObjectDisposedException`). After `Dispose`, every instance method (`GetPath`, `GetReachable`, `HasLineOfSight`, `BeginStepwiseSearch`, `EnablePathCaching`, `DisablePathCaching`, `InvalidateCache`, `Refresh`, `GetCell`, `GetIndex`, the `Set*` cell updates) throws `ObjectDisposedException`; the static distance helpers keep working.
- Uses unsafe code and array pooling internally to minimize garbage collection.
- The search state (scores, parents, open/closed) lives in the pathfinder, one node per cell, and is reset lazily: each query starts a new generation instead of clearing the grid, so a search costs the cells it touches, not the grid size. Queries never write into your cells.
- A cell carries no coordinate: its slot is its position, and every coordinate a query reports (path waypoints, `ReachableCell.Coordinate`, `SearchNode.Coordinate`) is computed from it.
- A `Cell[]` is searched in place: a query costs only the cells it searches, and direct writes to the array are seen by the next query. The `Cell[,]` and holder constructors search a snapshot of your grid (a `Cell[,]` is copied in one block). With the default [`GridSnapshotMode.PerQuery`](GridSnapshotMode.md) every query copies the whole grid first (O(grid size)); with `Manual` the copy happens once and after `Refresh()`, so short queries on a large grid stay cheap.
- The pathfinding algorithm is optimized for speed and memory efficiency.
- For larger grids, reuse the same `Pathfinder` instance for multiple path calculations.
- `GetPath` and `BeginStepwiseSearch` throw `ArgumentException` when `from` or `to` is outside the grid, and every query throws `ArgumentOutOfRangeException` when `agentSize < 1`.
- A destination the agent cannot enter (not walkable, occupied while `AreOccupiedCellsBlocking` is enabled, or without clearance for the agent's size) fails immediately without searching: `GetPath` returns a failure result and a `StepwiseSearch` starts complete in the `Failure` state. A query from a cell to itself always succeeds with an empty path.
- Settings are frozen at construction. To vary occupancy per query (units block for movement, not for a threat range) or to enter specific occupied cells (the mover's own cells, an attack target), pass [`QueryOptions`](QueryOptions.md) to the overloads that take it. `default(QueryOptions)` behaves exactly like the overloads without options, and the options never change the grid.
- Path caching is disabled by default but can be enabled for improved performance when the same paths are requested multiple times.

## Agent size

Every query takes the agent's size, the side of the square of cells it occupies, as `int agentSize`. It is always the **third** argument; the `QueryOptions` overloads take the options after it.

- `agentSize` defaults to 1 (a single-cell agent) in the overloads without options; the options overloads take it explicitly.
- `agentSize < 1` throws `ArgumentOutOfRangeException`. A bare `default` as the third argument is a size of 0, so `GetPath(from, to, default)` throws instead of meaning "default options".
- The [path cache](../guides/path-caching.md) is keyed by `(agentSize, from, to)`; a custom [`IPathCaching`](IPathCaching.md) receives the query's `agentSize`.

```csharp
using var path  = pathfinder.GetPath(from, to);                // single-cell agent
using var large = pathfinder.GetPath(from, to, agentSize: 2);  // 2x2 agent
using var range = pathfinder.GetReachable(from, 500, 2, new QueryOptions(OccupancyRule.Ignore));
```

## Cell updates

`SetWalkable`, `SetOccupied`, `SetWeight` and `SetCell` change one cell and keep the bookkeeping in the pathfinder:

- **`Cell[]` and `Cell[,]` constructors:** the write goes to your own array or matrix (the pathfinder keeps the reference), so your grid and the pathfinder stay in sync and the next query sees the change. O(1) and allocation-free: the cell at `c` is `cells[GetIndex(c)]` or `matrix[c.X, c.Y]`. Your array is never reordered.
- **`Cell[,]` constructor:** the setters also write the pathfinder's snapshot, so the change is seen at once under [`GridSnapshotMode.Manual`](GridSnapshotMode.md) too (no `Refresh()` needed).
- **`ICellHolder[]` and `ICellHolder[,]` constructors:** the setters throw `NotSupportedException`. The holders are the source of truth the pathfinder reads, so update the holder itself (and call `Refresh()` with `GridSnapshotMode.Manual`, and `InvalidateCache()` when caching is on). `GetCell` and `GetIndex` work in every mode.
- **Path cache:** a setter that changes a value cached paths depend on clears the cache (walkability always; occupancy only with `AreOccupiedCellsBlocking`; weight only with `IsCellWeightEnabled`). Setting the current value again leaves the cache alone. Direct writes to your grid still need `InvalidateCache()`.
- Out-of-grid coordinates throw `ArgumentException`. `SetWeight` and `SetCell` throw `ArgumentOutOfRangeException` for a negative weight.

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

using var result = pathfinder.GetPath(new Coordinate(1, 1), new Coordinate(8, 8));

// Cell updates through the pathfinder clear the cache themselves
pathfinder.SetWalkable(new Coordinate(4, 4), false);

// Clear the cache by hand after writing to your grid directly
pathfinder.InvalidateCache();

// Disable caching when no longer needed
pathfinder.DisablePathCaching();
```

### Movement range (reachability)

`GetReachable` returns every cell an agent can reach from an origin without exceeding a movement budget — useful for tactics/strategy games that highlight where a unit can move. It is a uniform-cost (Dijkstra) flood fill that honours the same movement rules as `GetPath` (diagonal movement, corner-cutting, occupied cells and agent clearance).

Per-step cost is `StraightStepCost` for cardinal moves and `DiagonalStepCost` for diagonal moves; when `IsCellWeightEnabled` is set, the destination cell's `Weight` is added to the step cost. Costs are integers: a cell whose cheapest route costs exactly the budget is included, one costing more is not. The origin is always included with cost `0`. Routes costing more than 1,073,741,823 (`int.MaxValue / 2`) are never reached, so `int.MaxValue` is an unbounded budget.

```csharp
using var pathfinder = new Pathfinder(cells, 10, 10);

// Every tile reachable within 4 movement points
using var range = pathfinder.GetReachable(new Coordinate(4, 4), 400);

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

using var search = pathfinder.BeginStepwiseSearch(new Coordinate(1, 1), new Coordinate(8, 8));

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

- `GetManhattanDistance(from, to)` — the taxicab distance `|dx| + |dy|` (cardinal steps).
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

The mode only governs walkability. Independently, an occupied cell blocks sight when `AreOccupiedCellsBlocking` is enabled — so "ignore terrain, but units still block" is expressible. The endpoints themselves are never tested, so a target standing on a blocked or occupied cell can still be "seen". Agent size is not considered — the check traces a single-cell ray. A cell always has line of sight to itself, and an out-of-range coordinate throws `ArgumentException`. 