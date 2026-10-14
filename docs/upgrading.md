# Upgrading MPath

What a caller has to know, and change, when moving from one released version to the next. Sections are newest first; each one is written for a caller coming from the previous **released** version, not from an intermediate `master` state.

New features that change nothing for existing code are not listed here; see the [GitHub releases](https://github.com/migus88/MPath/releases) for the full notes.

## 1.2.1 → 2.0

2.0 is the first release after 1.2.1. It changes what some existing calls return (listed under [Behaviour changes](#behaviour-changes)) and breaks parts of the API (listed under [Breaking changes](#breaking-changes)). Work through the [Checklist](#checklist) at the end.

### Behaviour changes

Each entry says what changed, who notices it, and what to do, if anything. Entries are listed even when they are not breaking, as long as a caller could notice them.

#### Path smoothing output changed

- **What changed:**
  - `PathSmoothingMethod.Simple` no longer repeats the first waypoint.
  - `Simple` keeps a turn right before the last step, where 1.2.1 cut that corner diagonally.
  - `PathSmoothingMethod.StringPulling` only takes shortcuts that the agent's size, occupied cells and the corner rule allow. 1.2.1 could shortcut through cells a large agent does not fit, through occupied cells, or across a blocked corner.
- **Who notices:** callers with smoothing enabled. Smoothed paths can have different waypoints, and a different `Length`, than in 1.2.1.
- **What to do:** nothing, unless you stored or asserted exact smoothed paths; regenerate those.

#### `GetPath` validates the origin and fails fast on a destination the agent cannot enter

- **What changed:**
  - `GetPath` throws `ArgumentException` (parameter `from`) for an origin outside the grid. 1.2.1 did not check it and read and wrote memory outside the grid.
  - A destination the agent cannot enter (not walkable, no room for the agent's size, or occupied while occupied cells block) returns a failed `PathResult` at once, without searching. The result is the same as in 1.2.1, only faster.
  - `from == to` still succeeds, even on such a cell.
  - A stepwise search (`BeginStepwiseSearch`) towards such a destination starts in the `SearchState.Failure` state.
- **Who notices:** callers that passed an origin outside the grid (now an exception instead of undefined behaviour), and stepwise visualizations of an unreachable destination (no expansion steps any more).
- **What to do:** validate origins before calling `GetPath`, or catch `ArgumentException`.

#### Corner cutting is configured by `CornerBlocking`

- **What changed:** the new `CornerBlocking` setting (flags) decides when a diagonal step between two blocked or occupied flanking cells is allowed. Its default, `CornerBlocking.BetweenBlockedCells`, reproduces 1.2.1's rule: a diagonal is blocked only when both flanking cells are blocked. 1.2.1's `IsMovementBetweenCornersEnabled = true` is `CornerBlocking.None`.
- **Who notices:** nobody, with the default settings.
- **What to do:** nothing now. The bool is removed in 2.0; see [Remove compatibility shims](#57-remove-compatibility-shims).

#### `Pathfinder.Dispose` is idempotent and final

- **What changed:**
  - Calling `Dispose` more than once is safe.
  - Every instance method throws `ObjectDisposedException` after `Dispose`.
  - Disposing the pathfinder ends its active stepwise search; that search's later `Tick()` calls throw `ObjectDisposedException`.
- **Who notices:** code that used a pathfinder, or its stepwise search, after disposing it.
- **What to do:** stop using a pathfinder once it is disposed; create a new one.

#### The path cache never hands out its own results

- **What changed:**
  - The caller owns, and must dispose, every `PathResult` that `GetPath` returns, cache hits included.
  - A cache hit returns a copy, which rents a pooled array.
  - A custom `IPathCaching` implementation receives a copy of each result to store, and owns it (it disposes it on eviction and on `Dispose`).
- **Who notices:** callers with `EnablePathCaching()` that did not dispose cache hits, or that kept using a result after the cache evicted it, and custom `IPathCaching` implementations.
- **What to do:** dispose every result (`using var path = pathfinder.GetPath(...)`). A custom cache disposes the results it stores when it drops them.

#### Queries with `QueryOptions` bypass the path cache

- **What changed:** the new `QueryOptions` overloads let a single query change the occupancy rule or make cells always enterable. A query whose options change the rules neither reads nor fills the path cache.
- **Who notices:** only callers of the new overloads.
- **What to do:** nothing.

#### Cell-update API

- **What changed:** the new `SetWalkable`, `SetOccupied`, `SetWeight`, `SetCell`, `GetCell` and `GetIndex` methods edit or read the grid through the pathfinder and clear the path cache when a change affects cached paths. The setters throw `NotSupportedException` for pathfinders built over `ICellHolder`s (holders are re-read on every query).
- **Who notices:** only callers of the new methods.
- **What to do:** nothing. Direct writes to your own grid still do not clear the path cache; call `InvalidateCache()` after them, as in 1.2.1.

#### `GetPath` returns a cheapest route

- **What changed:**
  - `GetPath` is now optimal: it returns a cheapest route. Routes, and their costs, can differ from 1.2.1.
  - A step costs the straight or diagonal multiplier **plus** the entered cell's `Weight` (when `IsCellWeightEnabled`), so `GetPath` and `GetReachable` agree on what a route costs.
  - The `ScoreH` and `ScoreF` values reported by the stepwise search differ.
- **Who notices:** callers that compare routes with stored ones, and stepwise visualizations that show the scores.
- **What to do:** nothing, unless you stored or asserted exact routes or scores; regenerate those.

#### Open set sized to the grid

- **What changed:** the open set is a heap rented once per pathfinder and sized to the grid (8 bytes per cell). The memory is taken when the pathfinder is created instead of growing during a search. `InitialBufferSize` is ignored.
- **Who notices:** callers that tuned `InitialBufferSize`, or measure memory at construction.
- **What to do:** nothing now. `InitialBufferSize` is removed in 2.0; see [Remove compatibility shims](#57-remove-compatibility-shims).

#### Search state moved out of `Cell`

- **What changed:**
  - Searches never write into the caller's cells.
  - `Cell` shrank from 56 to 20 bytes and lost its internal search fields (8 bytes in 2.0; see [Coordinate cleanup](#58-coordinate-cleanup) and [Drop Coordinate from the cell](#61-drop-coordinate-from-the-cell)).
  - The pathfinder rents 24 bytes per cell of search state when it is created.
  - The new `GridSnapshotMode` setting decides when a matrix or holder pathfinder copies the grid. The default, `PerQuery`, copies it before every query, as in 1.2.1; `Manual` copies it once and again only after the new `Refresh()`.
  - `Cell.Reset()` does nothing.
- **Who notices:** code that reflects over `Cell`'s internal fields or assumes its layout (unsafe code, binary serializers), and code that called `Cell.Reset()`.
- **What to do:** stop relying on `Cell`'s internal fields or size. `Cell.Reset()` is removed in 2.0; see [Remove compatibility shims](#57-remove-compatibility-shims).

#### Deterministic tie-breaking

- **What changed:** the new `TieBreaking` setting picks among equal-cost routes (`FewerDiagonals`, `FewerTurns`). The default, `None`, gives the same routes as the search without tie-breaking. `PathResult` and `ReachableCell` gained `Steps` and `DiagonalSteps`.
- **Who notices:** nobody, with the default settings.
- **What to do:** nothing.

#### A `Cell[]` grid is never sorted or reordered

- **What changed:**
  - 1.2.1 sorted the caller's `Cell[]` in place into column-major order. Now the pathfinder never writes the order of your array: the new `CellLayout` setting (`ColumnMajor`, the default, or `RowMajor`) says which index holds which cell, and the array is searched in place, without a copy.
  - `Refresh()` also clears the path cache.
  - A `Cell[]` shorter than `width * height` throws `ArgumentException` when the pathfinder is created.
- **Who notices:** code that relied on 1.2.1 re-sorting the array, and code that passed an array shorter than the grid.
- **What to do:** see [Drop Coordinate from the cell](#61-drop-coordinate-from-the-cell): in 2.0 a cell's index is its position, so the array must be in the declared order.

#### Faster results API

- **What changed:**
  - `PathResult` and `RangeResult` gained `AsSpan()`, an indexer and a struct `Enumerator`, so `foreach (var c in result)` allocates nothing.
  - `RangeResult.Contains`, `TryGetCost` and `TryGetCell` are O(1) through an index the result rents on its first lookup, so that first call takes memory (returned on `Dispose`).
  - New `RangeResult.CopyCostsTo`.
- **Who notices:** callers that measure allocations around the first lookup on a `RangeResult`.
- **What to do:** nothing. Prefer `foreach` over the result itself, or `AsSpan()`, to the allocating `Path` and `Cells` iterators.

#### Not behaviour changes

`c022205` (docs: the AI agent reference, `docs/llms.md`) and `7c7026c` (the sample project moved to Unity 6.4; the package's minimum Unity version stays 2022.3) change nothing for callers.

### Breaking changes

Each entry has a description and a before → after migration snippet.

#### #56 Int costs everywhere

- **What changed:**
  - Costs are `int` everywhere: there are no floats left in the cost API, and no hidden fixed-point scale. You pick the resolution (10/14, 100/141, 1000/1414, ...) and every cost uses it: step costs, cell weights, budgets and every reported cost.
  - `IPathfinderSettings.StraightMovementMultiplier` / `DiagonalMovementMultiplier` (`float`) are renamed `StraightStepCost` / `DiagonalStepCost` (`int`) on `IPathfinderSettings`, `PathfinderSettings` and `ScriptablePathfinderSettings`. They were never multipliers: the cell's weight is added to them. The defaults are **100** and **141**, the same 1 : 1.41 ratio as 1.2.1, so default routes do not change and costs read ×100.
  - `Cell.Weight`, `Pathfinder.SetWeight`'s `weight`, every `GetReachable` `budget`, `ReachableCell.Cost`, `RangeResult.TryGetCost`'s `out` value, `RangeResult.CopyCostsTo` (`Span<int>`, `int unreached = -1`) and `SearchNode.ScoreG` / `ScoreH` / `ScoreF` are `int`.
  - The `ReachableCell(Coordinate, float cost)` constructor is removed; use `ReachableCell(Coordinate, int cost, int steps, int diagonalSteps)`.
  - Validation: the pathfinder constructor throws `ArgumentOutOfRangeException` when a step cost is below 1, or when `(width + height) * the larger step cost in use` exceeds 1,073,741,823 (`int.MaxValue / 2`). `SetWeight` and `SetCell` throw `ArgumentOutOfRangeException` for a negative weight; a negative weight written straight into your grid counts as 0.
  - Overflow headroom: a route whose cost would pass 1,073,741,823 is never taken, so the cells behind it are unreachable; weights above it count as that value. Nothing wraps around, and `GetReachable(from, int.MaxValue)` is an unbounded budget.
  - A budget equal to a route's cost admits it and one below excludes it, with no tolerance. With `TieBreaking.None`, routes of equal cost now tie exactly (float sums used to split them by summation order), so the search may return a different one of several equally cheap routes. Under `FewerDiagonals` and `FewerTurns` routes are unchanged at the same ratio.
- **Who notices:** every caller that sets step costs or weights, passes a budget, or reads a cost; custom `IPathfinderSettings` implementations; `ScriptablePathfinderSettings` assets.
- **What to do:** pick a resolution and scale every cost by it. `ScriptablePathfinderSettings` assets do not carry the old float fields over: enter the step costs again at your resolution (the library cannot know your scale). Callers that used 1/1 costs with `FewerDiagonals` to count steps can keep `StraightStepCost = DiagonalStepCost = 1`.

```csharp
// 1.2.1
var settings = new PathfinderSettings { StraightMovementMultiplier = 1f, DiagonalMovementMultiplier = 1.41f };
cell.Weight = 0.5f;
using var range = pathfinder.GetReachable(from, 4.23f);
range.TryGetCost(c, out float cost);
var costs = new float[width * height];
range.CopyCostsTo(costs);

// 2.0: pick a resolution (here x100) and scale every cost by it
var settings = new PathfinderSettings { StraightStepCost = 100, DiagonalStepCost = 141 };
cell.Weight = 50;
using var range = pathfinder.GetReachable(from, 423);
range.TryGetCost(c, out int cost);
var costs = new int[width * height];
range.CopyCostsTo(costs);
```

#### #57 Remove compatibility shims

Members that 1.2.1 had and 2.0 no longer reads are removed outright, and `IPathfinderSettings` has no default bodies.

- **`IsMovementBetweenCornersEnabled` is removed** from `IPathfinderSettings`, `PathfinderSettings` and `ScriptablePathfinderSettings`. Set `CornerBlocking` instead: `true` is `CornerBlocking.None`, `false` is `CornerBlocking.BetweenBlockedCells` (the default).
- **`InitialBufferSize` is removed** from the same three types (and `ScriptablePathfinderSettings.InitialBufferSizeSerialized`). The open set is sized to the grid, so there is nothing to replace it with.
- **Custom `IPathfinderSettings` implementations** must implement `CornerBlocking`, `GridSnapshotMode`, `CellLayout` and `TieBreaking`. To keep 1.2.1 behaviour, return `CornerBlocking.BetweenBlockedCells` (or `CornerBlocking.None` if your bool returned `true`), `GridSnapshotMode.PerQuery`, `CellLayout.ColumnMajor` (or `RowMajor`; see [Drop Coordinate from the cell](#61-drop-coordinate-from-the-cell)) and `TieBreaking.None`.
- **`ScriptablePathfinderSettings` assets saved with 1.2.1:** the old bool is no longer read. An asset that had **Is Movement Between Corners Enabled** ticked must set **Corner Blocking** to `None` (`Nothing` in the Inspector's flags field). Every other asset already gets the default, `BetweenBlockedCells`.
- **`Cell.Reset()` is removed.** Searches never write into cells, so there is nothing to reset.
- **`PathResult.Success(path, length)` is removed.** Callers that build results themselves (custom caches, test doubles) pass the step counts: `Success(path, length, steps, diagonalSteps)`.

```csharp
// 1.2.1
var settings = new PathfinderSettings { IsMovementBetweenCornersEnabled = true, InitialBufferSize = 512 };
cell.Reset();
var result = PathResult.Success(path, length);

// 2.0
var settings = new PathfinderSettings { CornerBlocking = CornerBlocking.None }; // false -> omit (BetweenBlockedCells is the default)
// InitialBufferSize: delete the line.
// cell.Reset(): delete the call.
var result = PathResult.Success(path, length, steps: length, diagonalSteps: 0);

// A custom IPathfinderSettings adds:
public CornerBlocking CornerBlocking => CornerBlocking.BetweenBlockedCells;
public GridSnapshotMode GridSnapshotMode => GridSnapshotMode.PerQuery;
public CellLayout CellLayout => CellLayout.ColumnMajor;
public TieBreaking TieBreaking => TieBreaking.None;
```

#### #58 Coordinate cleanup

- **What changed:**
  - `Coordinate` is a `readonly struct`: `X` and `Y` are get-only and set only by the constructor.
  - `IsInitialized` and `Reset()` are removed. `default(Coordinate)` is `(0,0)` and is the same value as `Coordinate.Zero` (they were already `==` in 1.2.1, but their `IsInitialized` differed).
  - `Coordinate` is 8 bytes instead of 12, so `PathResult` waypoint arrays are a third smaller (and `Cell` drops it altogether; see [Drop Coordinate from the cell](#61-drop-coordinate-from-the-cell)).
- **Who notices:** code that reads `IsInitialized` or calls `Reset()` (it no longer compiles), and code that assumes the size of `Coordinate` or `Cell` (unsafe code, binary serializers).
- **What to do:** to mean "no coordinate", use `Coordinate?` or your own sentinel; to clear a coordinate, assign a new value.

```csharp
// 1.2.1
if (!target.IsInitialized) { ... }
target.Reset();

// 2.0
Coordinate? target = null;
if (target is null) { ... }
target = null;
```

#### #59 Remove IAgent

Remove IAgent: agentSize-only overloads, int-keyed IPathCaching, no `default` trap.

- **What changed:**
  - `IAgent` is removed. Every query takes the agent's size as an `int agentSize`, always the **third** argument; the new `QueryOptions` overloads take the options after it:
    - `GetPath(from, to, int agentSize = 1)` and `GetPath(from, to, int agentSize, QueryOptions options)`;
    - `GetReachable(from, budget, int agentSize = 1)` and `GetReachable(from, budget, int agentSize, QueryOptions options)`;
    - `BeginStepwiseSearch(from, to, int agentSize = 1)` and `BeginStepwiseSearch(from, to, int agentSize, QueryOptions options)`.
  - `agentSize` below 1 throws `ArgumentOutOfRangeException` (parameter `agentSize`). In 1.2.1 an `IAgent` whose `Size` was below 1 was treated as a single-cell agent. A bare `default` as the third argument is a size of 0, so it throws too; there is no overload that takes options in that position.
  - `IPathCaching` keys on the size: `TryGetCachedPath(int agentSize, Coordinate from, Coordinate to, out PathResult pathResult)` and `CachePath(int agentSize, Coordinate from, Coordinate to, PathResult pathResult)`. `DefaultPathCaching` keys on `(agentSize, from, to)` as before; queries of different sizes never share an entry.
- **Who notices:** every caller that passes an agent, implements `IAgent` (the interface no longer exists), or implements `IPathCaching`.
- **What to do:** pass `agent.Size` as the third argument (or nothing, for a single-cell agent). Your own agent types can stay; they no longer implement a library interface. A custom cache keys on `agentSize` instead of `agent.Size`.

```csharp
// 1.2.1
using var path   = pathfinder.GetPath(agent, from, to);
using var range  = pathfinder.GetReachable(agent, from, budget);
using var search = pathfinder.BeginStepwiseSearch(agent, from, to);
class Unit : IAgent { public int Size => 2; }
class MyCache : IPathCaching { public bool TryGetCachedPath(IAgent agent, Coordinate from, Coordinate to, out PathResult result) => ... agent.Size ...; }

// 2.0
using var path   = pathfinder.GetPath(from, to, agent.Size);   // or GetPath(from, to) for a single-cell agent
using var direct = pathfinder.GetPath(from, to, agent.Size, options); // new in 2.0: options follow the size
using var range  = pathfinder.GetReachable(from, budget, agent.Size);
using var search = pathfinder.BeginStepwiseSearch(from, to, agent.Size);
class Unit { public int Size => 2; }                            // no interface
class MyCache : IPathCaching { public bool TryGetCachedPath(int agentSize, Coordinate from, Coordinate to, out PathResult result) => ... agentSize ...; }
```

#### #60 Rename IsCalculatingOccupiedCells

Rename IsCalculatingOccupiedCells to AreOccupiedCellsBlocking.

- **What changed:** `IPathfinderSettings.IsCalculatingOccupiedCells` is renamed to `AreOccupiedCellsBlocking`, on `PathfinderSettings` and `ScriptablePathfinderSettings` too. Same meaning, same default (`true`): occupied cells cannot be entered, count against an agent's clearance and the corner rule, and block line of sight. The new name pairs with the per-query `OccupancyRule.Block`.
- **Who notices:** code that sets or reads the property, and custom `IPathfinderSettings` implementations.
- **What to do:** rename the property. `ScriptablePathfinderSettings` assets keep their value (the field carries `FormerlySerializedAs`); re-saving an asset writes the new field name.

```csharp
// 1.2.1
new PathfinderSettings { IsCalculatingOccupiedCells = false };
public bool IsCalculatingOccupiedCells => true; // custom IPathfinderSettings

// 2.0
new PathfinderSettings { AreOccupiedCellsBlocking = false };
public bool AreOccupiedCellsBlocking => true;
```

#### #61 Drop Coordinate from the cell

- **What changed:**
  - `Cell` has no `Coordinate`: it is `IsWalkable`, `IsOccupied` and `Weight`, 8 bytes (20 in 1.2.1). A cell's position is its slot in the grid:
    - `Cell[,]` and `ICellHolder[,]`: the element `[x, y]`;
    - `Cell[]` and `ICellHolder[]`: the index under `CellLayout`, `x * height + y` (`ColumnMajor`, the default) or `y * width + x` (`RowMajor`).
  - Nothing is sorted, checked or placed by coordinate any more. In 1.2.1 every grid was placed by each cell's `Coordinate`: an array or matrix whose slots disagreed with the coordinates was silently moved, and two cells with the same coordinate left a slot holding whatever the pooled buffer held before.
  - A `null` holder is a non-walkable cell. 1.2.1 skipped it, so its slot kept stale data from the pooled buffer. `GetCell` on it returns a `default` (non-walkable) cell.
  - `CellLayout` keeps only `ColumnMajor = 1` (the default) and `RowMajor = 2`, and now also applies to `ICellHolder[]`. Any other value throws `ArgumentOutOfRangeException` when the pathfinder is created.
  - An `ICellHolder[]` shorter than `width * height` throws `ArgumentException` when the pathfinder is created, like a `Cell[]`.
  - `GetIndex` and `RangeResult.CopyCostsTo` always share one index space: the caller's layout for arrays, column-major for matrices. `GetIndex`, `GetCell` and the cell setters are O(1).
  - Path coordinates, `ReachableCell.Coordinate` and `SearchNode.Coordinate` are computed from the slot. `GetCell(Coordinate)` and `SetCell(Coordinate, in Cell)` keep their signatures: the coordinate is the argument.
- **Who notices:** every caller that builds cells (`Coordinate = ...` no longer compiles) or reads `cell.Coordinate`; callers whose array is not column-major, whose holders sit in slots other than their position, or whose holder grid has `null` holders or is shorter than the grid.
- **What to do:**
  - Delete `Coordinate = ...` from cell initializers; put each cell in its slot instead.
  - A `y * width + x` array (1.2.1 sorted it): set `CellLayout = CellLayout.RowMajor`.
  - An array in any other order (1.2.1 sorted it too): lay it out column- or row-major. An `ICellHolder[]` follows the same rule.
  - `ScriptablePathfinderSettings` assets saved with 1.2.1 get `ColumnMajor`; pick `RowMajor` in the Inspector for a row-major grid.
  - Code that read `cell.Coordinate` from `GetCell` or a holder already has the coordinate: it is the argument, or the slot.

```csharp
// 1.2.1
cells[x, y] = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };
rowMajor[y * width + x] = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };
class Tile : ICellHolder { public Cell CellData => new Cell { Coordinate = _position, IsWalkable = _isOpen }; }

// 2.0: the slot is the position (and Cell is PathCell; see #62)
cells[x, y] = new PathCell { IsWalkable = true };
rowMajor[y * width + x] = new PathCell { IsWalkable = true };
var pathfinder = new Pathfinder(rowMajor, width, height, new PathfinderSettings { CellLayout = CellLayout.RowMajor });
class Tile : ICellHolder { public PathCell CellData => new PathCell { IsWalkable = _isOpen }; } // its array slot is its position
```

#### #62 Rename Cell to PathCell

- **What changed:**
  - `Migs.MPath.Core.Data.Cell` is renamed `PathCell`, so it no longer clashes with the `Cell` type most games already have (`CS0104` when both namespaces are imported). Same members (`IsWalkable`, `IsOccupied`, `Weight`), same 8-byte layout, same behaviour.
  - Every public signature that took or returned `Cell` takes or returns `PathCell`: the `Pathfinder(PathCell[] cells, int fieldWidth, int fieldHeight, ...)` and `Pathfinder(PathCell[,] cellsMatrix, ...)` constructors, `GetCell`, `SetCell(Coordinate, in PathCell)` and `ICellHolder.CellData`.
  - Names that use "cell" as a concept do not change: `GetCell`, `SetCell`, `ReachableCell`, `ICellHolder`, `CellData`, `CellLayout`, `IsCellWeightEnabled`.
  - The other entries on this page name the type `Cell`, as 1.2.1 code does; their 2.0 snippets use `PathCell`.
- **Who notices:** every caller that names the type: grids built as `Cell[]` or `Cell[,]`, `ICellHolder` implementations, code that reads `GetCell` into a typed variable, and the `using GridCell = Migs.MPath.Core.Data.Cell;` alias workaround.
- **What to do:** replace the type name `Cell` with `PathCell` (a whole-word search and replace, in files that import `Migs.MPath.Core.Data`). The alias is no longer needed; to keep it, point it at `PathCell`. In Unity, the script keeps its `.meta` GUID, so nothing in scenes or prefabs needs re-linking.

```csharp
// 1.2.1
using Migs.MPath.Core.Data;
var grid = new Cell[width, height];
class Tile : ICellHolder { public Cell CellData => ...; }
using GridCell = Migs.MPath.Core.Data.Cell; // alias workaround

// 2.0
using Migs.MPath.Core.Data;
var grid = new PathCell[width, height];
class Tile : ICellHolder { public PathCell CellData => ...; }
// the alias is no longer needed; to keep it: using GridCell = Migs.MPath.Core.Data.PathCell;
```

### Checklist

1. Dispose every `PathResult`, `RangeResult` and `StepwiseSearch`, including path-cache hits.
2. Lay out a `Cell[]` or `ICellHolder[]` grid column-major, or row-major with `CellLayout = RowMajor`; `pathfinder.GetIndex` gives a cell's index.
3. Regenerate any stored or asserted routes, smoothed paths or stepwise scores.
4. Replace `IsMovementBetweenCornersEnabled` with `CornerBlocking`, including in `ScriptablePathfinderSettings` assets that had it ticked; delete `InitialBufferSize` and `Cell.Reset()` calls; pass step counts to `PathResult.Success`; add the four settings members to a custom `IPathfinderSettings`.
5. Replace `Coordinate.IsInitialized` and `Coordinate.Reset()` with a `Coordinate?` (or your own sentinel).
6. Pick a cost resolution and scale every cost to `int`: `StraightStepCost`/`DiagonalStepCost` (renamed from the multipliers), `Cell.Weight`, budgets, and the costs you read back. Re-enter the step costs on `ScriptablePathfinderSettings` assets.
7. Replace every `IAgent` argument with its size as the third argument (`GetPath(from, to, agent.Size)`), drop `: IAgent` from your types, and key a custom `IPathCaching` on `int agentSize`.
8. Rename `IsCalculatingOccupiedCells` to `AreOccupiedCellsBlocking`, including in custom `IPathfinderSettings` implementations.
9. Delete `Cell.Coordinate` from cell initializers and reads; put every cell (and holder) in its slot: `[x, y]` in a matrix, column-major in an array, or row-major with `CellLayout = RowMajor`.
10. Rename the type `Cell` to `PathCell` (whole word, in files that import `Migs.MPath.Core.Data`), and drop or retarget a `using GridCell = Migs.MPath.Core.Data.Cell;` alias.
