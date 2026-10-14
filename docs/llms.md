# MPath — AI Agent & LLM Reference

> **Audience: AI coding agents (and their humans) that want to *use* MPath in their own project.**
> This is a single, self-contained, machine-first page: the public API surface, the capabilities, the
> canonical usage patterns, and the non-obvious rules that cause bugs. Read it top-to-bottom and you can
> write correct MPath code without opening any other file. For full per-member signatures of a type, follow
> its link into [`docs/api/`](api/README.md).
>
> *(Modifying MPath's own source instead of using it? This page is not for you — see [`CLAUDE.md`](../CLAUDE.md).)*
>
> **Upgrading a caller from an older version?** Read [`docs/upgrading.md`](upgrading.md) first: it lists every behaviour and breaking change per release, with migrations.

---

## At a glance

| Fact | Value |
|------|-------|
| What it is | High-performance A* pathfinding for **2D grids** |
| Distributions | NuGet `Migs.MPath` · Unity UPM `com.migsweb.mpath` (OpenUPM) |
| Target framework | `netstandard2.1` |
| Core namespace | `Migs.MPath.Core` |
| Thread-safe? | **No.** One `Pathfinder` per thread; never share across threads. |
| Allocations | Near-zero on the hot path — uses `unsafe` pointers + `ArrayPool`. |
| Lifecycle rule | `Pathfinder` is **long-lived & reused** (dispose once). Each result (`PathResult` / `RangeResult` / `StepwiseSearch`) is **per-query & must be disposed** (`using`). |
| Grid indexing | A cell's slot is its position (`PathCell` has no coordinate): `[x, y]` in a matrix; in a flat `PathCell[]`/`ICellHolder[]` the index under `CellLayout`, column-major `x * Height + y` (default) or row-major `y * Width + x`. Queries never reorder it. |
| Coordinate origin | `(0,0)` is one corner; `X`/`Y` are non-negative grid indices, not world units. |

## Namespaces (what to `using`)

```csharp
using Migs.MPath.Core;            // Pathfinder
using Migs.MPath.Core.Data;       // PathCell, Coordinate, PathResult, RangeResult, ReachableCell,
                                  // PathfinderSettings, CornerBlocking, LineOfSightMode, PathSmoothingMethod,
                                  // QueryOptions, OccupancyRule,
                                  // SearchStep, SearchNode, SearchState, SearchNodeState
using Migs.MPath.Core.Interfaces; // ICellHolder, IPathfinderSettings, IPathCaching
using Migs.MPath.Core.Caching;    // DefaultPathCaching (only if you need it directly)
```

Unity-only glue lives in `Migs.MPath` (`PathfindingExtensions`: `Coordinate ↔ Vector2Int`) and
`Migs.MPath.Settings` (`ScriptablePathfinderSettings`, a `ScriptableObject` form of the settings). The
core library has **no** `UnityEngine` dependency and works in any .NET app.

## Install

```bash
dotnet add package Migs.MPath          # .NET
```

```
openupm add com.migsweb.mpath          # Unity (run in the project folder)
```

---

## Mental model — four building blocks

1. **`PathCell`** — one grid square, 8 bytes. Public, mutable fields: `IsWalkable`, `IsOccupied`, `Weight`; its
   position is its slot in the grid. *You* own the grid data; MPath reads it.
2. **Agent size** — the thing moving, as an `int agentSize` (in cells; `1` = one tile, the default; `> 1`
   triggers a clearance scan so the agent only fits where a `Size×Size` block is walkable). Every query
   takes it as its **third** argument (optional, default `1`); `QueryOptions` go after it. There is no
   agent interface: keep your own unit types and pass their size.
3. **`Pathfinder`** — the engine. Build it once from your grid + settings, reuse it for every query,
   dispose it at the end of its life.
4. **A result type per query** — `PathResult`, `RangeResult`, or `StepwiseSearch`. All are `IDisposable`
   and **must** be disposed (they rent pooled arrays / pin memory). Always `using`.

`Coordinate` is an immutable 8-byte `readonly struct`: `new Coordinate(x, y)`, with `==`/`!=`/`+`/`-` operators and implicit
conversion to `(int x, int y)` (Unity callers can use `vector2Int.ToCoordinate()` /
`coordinate.ToVector2Int()`). `default(Coordinate)` is `(0,0)` and equals `Coordinate.Zero`; there is no
"uninitialized" flag, so use `Coordinate?` to mean "no coordinate".

---

## The 30-second example

```csharp
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

// 1. Build a grid (matrix form is simplest). PathCell fields default to ZERO — set them explicitly.
var grid = new PathCell[10, 10];
for (int x = 0; x < 10; x++)
for (int y = 0; y < 10; y++)
    grid[x, y] = new PathCell { IsWalkable = true, Weight = 0 }; // the slot [x, y] is the position

grid[3, 3].IsWalkable = false; // a wall

// 2. Create the pathfinder ONCE and reuse it. Settings are optional (sensible defaults).
using var pathfinder = new Pathfinder(grid);

// 3. Query (single-cell agent by default; pass 2 as the third argument for a 2x2 unit). Dispose every result.
using var result = pathfinder.GetPath(from: new Coordinate(0, 0), to: new Coordinate(9, 9));

if (result.IsSuccess)
{
    // The path does NOT include the origin; it is the sequence of steps after `from`.
    foreach (Coordinate step in result) { /* move to step */ }   // struct enumerator: no allocation
}
```

---

## Public API surface

Full per-member signatures for every type live in [`docs/api/`](api/README.md) (one page per type). This
section gives the entry point in full and maps the rest, so it stays small as the API grows.

### `Pathfinder` — the entry point (`sealed`, `IDisposable`)

**Constructors** (pick the one matching your grid representation; `settings` is optional):

```csharp
Pathfinder(PathCell[]         cells,             int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
Pathfinder(ICellHolder[]  holders,           int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
Pathfinder(PathCell[,]        cellsMatrix,                                        IPathfinderSettings settings = null)
Pathfinder(ICellHolder[,] cellHoldersMatrix,                                 IPathfinderSettings settings = null)
```

Matrix constructors infer width/height from the array. The flat `PathCell[]` form keeps a reference to your array
and never reorders or writes it during queries: it is searched in place (no copy), column-major (`x * Height + y`,
the default) or row-major (`y * Width + x`) as `CellLayout` says. The others flatten into a pooled snapshot — before
every query by default, or once and after `Refresh()` with `GridSnapshotMode.Manual`; a `null` holder is a wall, and
an `ICellHolder[]` follows `CellLayout` like a `PathCell[]`. A flat array shorter than `width * height` throws. `ICellHolder` lets you back cells with your own
objects (e.g. Unity `MonoBehaviour`s) via `PathCell CellData { get; }`. Queries never write into your cells: the search
state lives in the pathfinder and is reset lazily, so a short search costs only the cells it touches.

**Methods:**

Every query takes `int agentSize` as its **third** argument (default `1`; `< 1` throws
`ArgumentOutOfRangeException`, so a bare `default` there throws too). The `QueryOptions` overloads take the
options after it.

| Signature | Returns | Notes |
|-----------|---------|-------|
| `GetPath(Coordinate from, Coordinate to, int agentSize = 1)` | `PathResult` | Core A* query; returns a cheapest route for an `agentSize`×`agentSize` agent. Throws `ArgumentException` if `from` or `to` is outside the grid. Returns a **failure** result (not an exception) when no path exists; a destination the agent can't enter (unwalkable, occupied while `AreOccupiedCellsBlocking`, or no clearance for its size) fails immediately without searching, unless `from == to`. |
| `GetPath(Coordinate from, Coordinate to, int agentSize, QueryOptions options)` | `PathResult` | Same, with per-query rules (see [Per-query options](#per-query-options-units-block-or-not-enter-specific-occupied-cells)). Options that change the rules bypass the path cache. |
| `GetReachable(Coordinate from, int budget, int agentSize = 1)` | `RangeResult` | Every cell reachable for total cost `<= budget` ("where can this unit move?"). Uniform-cost flood fill. |
| `GetReachable(Coordinate from, int budget, int agentSize, QueryOptions options)` | `RangeResult` | Same, with per-query rules (`IsDestinationAlwaysEnterable` is ignored: no destination). |
| `HasLineOfSight(Coordinate from, Coordinate to, LineOfSightMode mode = BlockedByUnwalkableCells)` | `bool` | Single-cell Bresenham ray. Endpoints not tested; agent size ignored. |
| `static GetManhattanDistance(Coordinate from, Coordinate to)` | `int` | `|dx| + |dy|`. Pure metric, allocation-free. |
| `static GetChebyshevDistance(Coordinate from, Coordinate to)` | `int` | `max(|dx|, |dy|)`. Pure metric, allocation-free. |
| `BeginStepwiseSearch(Coordinate from, Coordinate to, int agentSize = 1)` | `StepwiseSearch` | Educational/visualization driver running the same A* one expansion at a time. **Pins the grid until disposed** — the pathfinder can't serve other queries meanwhile, and a second concurrent session throws. |
| `BeginStepwiseSearch(Coordinate from, Coordinate to, int agentSize, QueryOptions options)` | `StepwiseSearch` | Same, with per-query rules applied on every tick. |
| `SetWalkable(Coordinate c, bool isWalkable)` · `SetOccupied(Coordinate c, bool isOccupied)` · `SetWeight(Coordinate c, int weight)` | `void` | Update one cell (`SetWeight`/`SetCell` throw `ArgumentOutOfRangeException` for a negative weight). Writes into your `PathCell[]`/`PathCell[,]` (O(1), allocation-free) and clears the path cache when the change can affect cached paths. **Throws `NotSupportedException` in `ICellHolder` modes** (update the holder). `ArgumentException` outside the grid. |
| `SetCell(Coordinate c, in PathCell cell)` | `void` | Same, all three fields at once. |
| `GetCell(Coordinate c)` | `PathCell` | Current cell read from your grid, every mode (a `null` holder reads as `default`, a wall). |
| `GetIndex(Coordinate c)` | `int` | `PathCell[]`/`ICellHolder[]`: the cell's index in your array by its `CellLayout` (`X * fieldHeight + Y` or `Y * fieldWidth + X`). Matrices: `X * fieldHeight + Y`. The same index `RangeResult.CopyCostsTo` uses. |
| `EnablePathCaching(IPathCaching handler = null)` | `Pathfinder` | Fluent (returns `this`). Opt-in. Pass your own `IPathCaching` or use the built-in `DefaultPathCaching`. |
| `DisablePathCaching()` | `Pathfinder` | Fluent. |
| `InvalidateCache()` | `Pathfinder` | Fluent. The `Set*` cell updates and `Refresh()` invalidate for you; **direct writes to your grid or holders do NOT** — call this (or `Refresh()`) after them. |
| `Refresh()` | `Pathfinder` | Fluent. "The grid changed behind my back": the next query copies the snapshot again (`PathCell[,]`, holders), and the path cache is cleared. Needed after direct edits with `GridSnapshotMode.Manual` or caching on. |
| `Dispose()` | `void` | Returns the rented buffers (cells, search state, open set) to the pool, disposes the cache, ends an active `StepwiseSearch`. Idempotent; every instance method throws `ObjectDisposedException` afterwards. |

### Everything else — type map

One row per type; click through for full members. The load-bearing facts are repeated in
[Capabilities & recipes](#capabilities--recipes) and [Critical rules & gotchas](#critical-rules--gotchas-the-bug-causers).

| Type | Kind | Gist | Details |
|------|------|------|---------|
| `PathResult` | result, `IDisposable` | `IsSuccess`, `Length`, `foreach` / `AsSpan()` / `this[i]` (steps, **origin excluded**; allocation-free), `Path` (allocating iterator), `Get(i)`, `Steps` / `DiagonalSteps` (ints, of the route before smoothing) | [api](api/PathResult.md) |
| `RangeResult` | result, `IDisposable` | `IsSuccess`, `Length`, `foreach` / `AsSpan()` / `this[i]` (allocation-free), `Cells` (allocating iterator), `Contains(c)` / `TryGetCost(c, out cost)` / `TryGetCell(c, out cell)` (O(1)), `CopyCostsTo(Span<int>, unreached = -1)`, `Get(i)` | [api](api/RangeResult.md) |
| `ReachableCell` | `readonly struct` | `Coordinate` + `Cost` + `Steps` + `DiagonalSteps` (ints) | [api](api/ReachableCell.md) |
| `StepwiseSearch` | session, `IDisposable` | `Tick()`, `RunToCompletion()`, `State`, `IsComplete` | [api](api/StepwiseSearch.md) |
| `SearchStep` | immutable class | Per-tick snapshot: `Searched`, `Open/ClosedCount`, `Current`, `Iteration`, `Path` | [api](api/SearchStep.md) |
| `SearchNode` | `readonly struct` | `Coordinate` + `State` + `ScoreG/ScoreH/ScoreF` | [api](api/SearchNode.md) |
| `QueryOptions` | `readonly ref struct` | Per-query rules: `Occupancy` (`OccupancyRule`), `AlwaysEnterable` (`ReadOnlySpan<Coordinate>`), `IsDestinationAlwaysEnterable`; `default` = no override | [api](api/QueryOptions.md) |
| `PathCell` | `struct` | Grid square: `IsWalkable`, `IsOccupied`, `Weight` — **all default to zero**; its slot is its position | [api](api/PathCell.md) |
| `Coordinate` | `readonly struct` (8 B) | `X`, `Y`, `static Zero` (`== default`); ctor `(x,y)`; `== != + -`; `↔ (int,int)` | [api](api/Coordinate.md) |
| `ICellHolder` | interface | `PathCell CellData` — back cells with your own objects | [api](api/ICellHolder.md) |
| `IPathfinderSettings` · `PathfinderSettings` | settings | See [Settings reference](#settings-reference) below | [api](api/PathfinderSettings.md) |
| `IPathCaching` · `DefaultPathCaching` | caching | `TryGetCachedPath(int agentSize, from, to, out result)` / `CachePath(int agentSize, from, to, result)` / `ClearCache` | [api](api/IPathCaching.md) |

### Enums

| Enum | Values |
|------|--------|
| `GridSnapshotMode` | `PerQuery` (0, default) · `Manual` (1) — [api](api/GridSnapshotMode.md) |
| `CellLayout` | `ColumnMajor` (1, default) · `RowMajor` (2); any other value throws at construction — [api](api/CellLayout.md) |
| `CornerBlocking` (`[Flags]`) | `None` (0) · `WallAndWall` (1) · `WallAndOccupied` (2) · `OccupiedAndOccupied` (4) · `WallAndOpen` (8) · `OccupiedAndOpen` (16); presets `BetweenBlockedCells` (default) · `BetweenWalls` · `AnyWall` · `All` — [api](api/CornerBlocking.md) |
| `LineOfSightMode` | `BlockedByUnwalkableCells` (0, default) · `IgnoreUnwalkableCells` (1) |
| `OccupancyRule` | `UseSettings` (0, default) · `Block` (1) · `Ignore` (2) — [api](api/OccupancyRule.md) |
| `PathSmoothingMethod` | `None` (0) · `Simple` (1) · `StringPulling` (2) |
| `TieBreaking` | `None` (0, default) · `FewerDiagonals` (1) · `FewerTurns` (2) — [api](api/TieBreaking.md) |
| `SearchState` | `InProgress` · `Success` · `Failure` |
| `SearchNodeState` | `Open` · `Closed` |

---

## Settings reference

`PathfinderSettings` (`: IPathfinderSettings`). Construct with object-initializer syntax and pass to a
`Pathfinder` constructor. Converted once into an internal fast struct, so changing the instance after
construction has **no effect** — build a new `Pathfinder` to change settings. A custom
`IPathfinderSettings` must implement every member below (no default bodies); return these defaults for the
ones it doesn't care about.

| Property | Type | Default | Meaning |
|----------|------|---------|---------|
| `IsDiagonalMovementEnabled` | `bool` | `true` | Allow 8-direction movement. |
| `AreOccupiedCellsBlocking` | `bool` | `true` | Treat `IsOccupied` cells as blocked (for paths *and* line of sight). |
| `CornerBlocking` | `CornerBlocking` (`[Flags]`) | `BetweenBlockedCells` | Which flanking cells block a diagonal step: walls and occupied cells separately. See below. |
| `IsCellWeightEnabled` | `bool` | `true` | Add per-cell `Weight` to movement cost (terrain cost). |
| `StraightStepCost` | `int` | `100` | Cost of a cardinal step, at least 1. All costs are `int` in a unit you pick (default: hundredths of a step); weights, budgets and reported costs use it. |
| `DiagonalStepCost` | `int` | `141` | Cost of a diagonal step (1.41 straight steps), at least 1. |
| `PathSmoothingMethod` | `PathSmoothingMethod` | `None` | Post-process the path (`Simple`: first step + turn points + destination; `StringPulling`: straight shortcuts that keep the agent's footprint and the corner rule). |
| `GridSnapshotMode` | `GridSnapshotMode` | `PerQuery` | `PathCell[,]`/`ICellHolder` grids: copy the grid before every query (`PerQuery`, O(grid) per query) or once and after `Refresh()` (`Manual`, O(cells searched)). `Set*` updates are seen at once in both. |
| `CellLayout` | `CellLayout` | `ColumnMajor` | `PathCell[]`/`ICellHolder[]` only: which index holds cell (X, Y) — `ColumnMajor` (`X * fieldHeight + Y`) or `RowMajor` (`Y * fieldWidth + X`). The index is the position: nothing is checked or reordered. |
| `TieBreaking` | `TieBreaking` | `None` | Which route wins among equal-cost routes: `FewerDiagonals` (then fewer steps) or `FewerTurns`, with a fixed final rule. `None` = the fastest search; ties resolve by search order. |

> **Weight is additive, not multiplicative.** With `IsCellWeightEnabled`, step cost = straight/diagonal step cost
> **+** destination cell `Weight` (an `int` in the same unit). Because `Weight` defaults to `0`, a flat default grid behaves like
> uniform cost. Set higher `Weight` for slow terrain (mud, hills).
>
> **`GetPath` returns a cheapest route.** Its cost (sum of step costs above) equals the `GetReachable` cost of
> the destination. Costs are exact integers. With `TieBreaking.None`, ties between
> equal-cost routes resolve by search order (can zig-zag, can change with unrelated grid edits); set
> `TieBreaking` to pick a deterministic winner.
>
> **Costs are `int` and validated.** The constructor throws `ArgumentOutOfRangeException` when a step cost is
> below 1 or `(width + height) * the larger step cost` exceeds 1,073,741,823 (`int.MaxValue / 2`). A negative
> `Weight` written into your grid counts as 0. A route whose cost would pass 1,073,741,823 is never taken (cells
> behind huge weights are unreachable; nothing wraps). A budget equal to a route's cost admits it.

---

## Capabilities & recipes

### Find a path

```csharp
using var result = pathfinder.GetPath(from, to);                 // single-cell agent
using var big    = pathfinder.GetPath(from, to, 2);              // 2x2 agent: the size is always the third argument
if (result.IsSuccess)
    foreach (var step in result) { /* ... */ }        // origin excluded; or result.AsSpan() / result[i]
```

### Movement range ("where can this unit move?", tactics games)

```csharp
using var range = pathfinder.GetReachable(origin, budget: 500);
foreach (var c in range)                              // struct enumerator: no allocation
    Highlight(c.Coordinate, c.Cost);                  // every cell with cheapest cost <= 500 (5 straight steps)
if (range.TryGetCost(target, out var cost)) { /* reachable for `cost` */ }   // O(1), no allocation

var costs = new int[width * height];                  // reuse it across queries
range.CopyCostsTo(costs);                             // costs[pathfinder.GetIndex(c)], -1 where unreached
```

Per-step cost mirrors the main A*: straight/diagonal step cost **+** cell `Weight` (when weighting is on).
Each `ReachableCell` also carries integer `Steps` / `DiagonalSteps` of its cheapest route.

### Straight lines and step counts (deterministic ties)

```csharp
var settings = new PathfinderSettings
{
    StraightStepCost = 1, DiagonalStepCost = 1,                        // king moves: a diagonal costs 1
    IsCellWeightEnabled = false,
    TieBreaking = TieBreaking.FewerDiagonals                           // straight lines win ties
};
using var pathfinder = new Pathfinder(cells, settings);
using var path = pathfinder.GetPath(from, to);   // path.Steps, path.DiagonalSteps: exact ints
using var range = pathfinder.GetReachable(from, budget: 3);
if (range.TryGetCell(target, out var cell)) { /* cell.Steps, cell.DiagonalSteps */ }
```

`TieBreaking.FewerTurns` instead returns the cheapest route with the fewest direction changes. With any
tie-break, the route is a function of the grid, not of search order.

### Distance & line of sight (range checks, visibility — no path computed)

```csharp
int dist = Pathfinder.GetManhattanDistance(a, b);     // or GetChebyshevDistance — static, allocation-free
bool clearShot = pathfinder.HasLineOfSight(shooter, target);
// See-through terrain that blocks movement but not vision (water/glass/pit):
bool visible = pathfinder.HasLineOfSight(shooter, target, LineOfSightMode.IgnoreUnwalkableCells);
```

Occupancy is orthogonal to the mode: with `AreOccupiedCellsBlocking`, an occupied cell blocks sight in
*both* modes (so "ignore terrain, but units still block" is expressible).

### Visualize the search (teaching/demos only — use `GetPath` in production)

```csharp
using var search = pathfinder.BeginStepwiseSearch(from, to);
while (!search.IsComplete)
{
    var step = search.Tick();
    Render(step.Searched);                            // Open (frontier) vs Closed (expanded), with A* scores
}
if (search.State == SearchState.Success)
    DrawPath(search.RunToCompletion().Path);
```

### Path caching (opt-in)

```csharp
pathfinder.EnablePathCaching();                       // fluent; reuses identical (agent size, from, to) results
using var hit = pathfinder.GetPath(from, to);         // a hit is a fresh copy: still yours to dispose
pathfinder.SetWalkable(wall, false);                  // cell updates clear the cache themselves
// ... after writing to your grid or holders directly:
pathfinder.InvalidateCache();                         // NOT automatic for direct writes
```

### Corner-cutting: walls vs units

A diagonal step passes between two flanking cells, each open, a wall (unwalkable/off-grid) or occupied
(`IsOccupied` with `AreOccupiedCellsBlocking`). `CornerBlocking` flags pick which pairs block the step:

```csharp
var settings = new PathfinderSettings
{
    AreOccupiedCellsBlocking = true,           // units can't be entered...
    CornerBlocking = CornerBlocking.BetweenWalls // ...but only two walls block a diagonal
};
```

Applies to `GetPath`, `GetReachable`, stepwise search and `StringPulling` smoothing. For `Size > 1` a flank is the agent's footprint
(wall if any cell is a wall, else occupied if any is occupied). `HasLineOfSight` ignores it.

### Per-query options: units block or not, enter specific occupied cells

Settings are frozen, but each `GetPath` / `GetReachable` / `BeginStepwiseSearch` has an overload taking a
`QueryOptions` (`readonly ref struct`, allocation-free; `default` = exactly the overload without options):

```csharp
// Movement range with units blocking, threat range ignoring them - same pathfinder, same grid.
using var move   = pathfinder.GetReachable(unitCell, 500);
using var threat = pathfinder.GetReachable(unitCell, 500, new QueryOptions(OccupancyRule.Ignore));

// Move to attack an occupied cell; the mover's own (occupied) cells don't block it either.
ReadOnlySpan<Coordinate> own = stackalloc Coordinate[] { unitCell };
using var path = pathfinder.GetPath(unitCell, target,
    new QueryOptions(alwaysEnterable: own, isDestinationAlwaysEnterable: true));
```

An `AlwaysEnterable` cell is treated as unoccupied for that query only (entry, `Size > 1` clearance, open
flank for `CornerBlocking`); walls stay walls; the grid is never modified. `OccupancyRule.Block`/`Ignore`
override `AreOccupiedCellsBlocking` for the query. `HasLineOfSight` takes no options.

### Update cells as the game changes (doors, units moving)

```csharp
pathfinder.SetOccupied(unit.Position, false);         // O(1), writes into your PathCell[] / PathCell[,]
pathfinder.SetOccupied(newPosition, true);            // clears the path cache only if the value changed
pathfinder.SetWalkable(door, isOpen);
PathCell cell = pathfinder.GetCell(door);                 // reads your grid, every mode
```

Works for the `PathCell[]` (any order) and `PathCell[,]` constructors. With `ICellHolder` constructors the holders
are the source the pathfinder reads, so change the holder instead (the setters throw `NotSupportedException`).

**Many short queries on a large grid** (ranges per unit, AI turns, simulations): with a `PathCell[,]` or holder grid,
set `GridSnapshotMode.Manual` so queries stop copying the whole grid; then `Set*` updates are seen at once, and
after a direct edit to the matrix or a holder call `Refresh()` before the next query:

```csharp
var settings = new PathfinderSettings { GridSnapshotMode = GridSnapshotMode.Manual };
using var pathfinder = new Pathfinder(matrix, settings);
pathfinder.SetOccupied(c, true);                      // seen at once
matrix[x, y].IsWalkable = false; pathfinder.Refresh(); // direct edit: Refresh (also clears the cache)
```

**Your own flat array** (row-major `y * width + x` is fine): declare the layout and the array is searched in
place, never reordered, so your indexing stays valid and direct writes are seen by the next query:

```csharp
var cells = new PathCell[width * height];                 // cells[y * width + x]: the index is the position
var settings = new PathfinderSettings { CellLayout = CellLayout.RowMajor };
using var pathfinder = new Pathfinder(cells, width, height, settings);
cells[y * width + x].IsWalkable = false;              // seen by the next query (Refresh() if caching is on)
```

### Multi-cell agents & terrain cost

- `Size > 1`: the agent only traverses cells where the whole `Size×Size` block is walkable.
- Set `PathCell.Weight` for terrain cost and keep `IsCellWeightEnabled = true`.

### Unity

```csharp
using Migs.MPath;                                     // PathfindingExtensions
Coordinate c = someVector2Int.ToCoordinate();
Vector2Int v = c.ToVector2Int();
```

Use a `ScriptablePathfinderSettings` asset (Create ▸ MPath ▸ Pathfinder Settings) as the
`IPathfinderSettings`, and back cells with components implementing `ICellHolder`.

---

## Critical rules & gotchas (the bug-causers)

1. **Dispose every result.** `PathResult`, `RangeResult`, and `StepwiseSearch` rent pooled arrays / pin
   memory. Always `using`. Leaking them defeats the zero-allocation design and (for `StepwiseSearch`)
   keeps the grid pinned and the pathfinder unusable.
2. **Reuse the `Pathfinder`; don't recreate it per query.** It is designed as a long-lived instance.
   Dispose it once when you're done with it (or its grid). A second `Dispose()` is a no-op; any query after
   it throws `ObjectDisposedException`. Every constructor rents the search state (24 bytes per cell) and the open-set heap
   (8 bytes per cell) from the pool, and the `PathCell[,]` / `ICellHolder[]` / `ICellHolder[,]` constructors also rent a cell buffer; skip
   `Dispose` and that memory is collected instead of going back to the pool. Queries allocate nothing but
   their result, however large the search.
3. **Not thread-safe.** No concurrent queries on one instance, no sharing across threads. One per thread.
4. **`Path` excludes the origin.** It is the sequence of steps *after* `from`. Don't expect `from` first.
   Spans from `AsSpan()` and `foreach` enumerators read the result's pooled array: don't use them after
   disposing the result. `RangeResult` lookups stay valid until the result is disposed, whatever the pathfinder
   does meanwhile (later queries, `Dispose`).
5. **No path → failure result, not an exception.** Check `IsSuccess`. Only bad arguments throw
   (`agentSize < 1`; `from` or `to` outside the grid).
6. **Direct grid edits aren't seen by the cache.** The pathfinder re-reads your grid each query (with
   `GridSnapshotMode.Manual`, only after `Refresh()`), but cached results are returned verbatim — call `Refresh()` (or `InvalidateCache()`) after writing to your grid or holders
   directly if caching is on. `SetWalkable`/`SetOccupied`/`SetWeight`/`SetCell` invalidate for you. Cache hits are fresh copies owned by the caller: dispose them like any other result.
7. **Settings are frozen at construction.** Mutating the `PathfinderSettings` object afterward does
   nothing — create a new `Pathfinder`. To vary occupancy per query, pass `QueryOptions` instead of
   flipping `IsOccupied` around the call.
8. **A cell's slot is its position.** `PathCell` has no coordinate. A `PathCell[]`/`ICellHolder[]` must be column-major
   (`x * height + y`, the default) or row-major (`y * width + x`, set `CellLayout = RowMajor`); nothing checks it,
   so a row-major array under the default layout is silently transposed. Queries never sort or write it.
9. **`PathCell` fields default to zero.** `IsWalkable` defaults to `false` — initialize every cell or the
   whole grid is unwalkable.
10. **Stepwise search is exclusive.** While a `StepwiseSearch` is alive, the owning pathfinder throws on
    any other query. Dispose it promptly.
11. **The third argument is always the agent size.** Options go fourth:
    `GetPath(from, to, 1, options)`. A bare `default` in the third position is a size of 0 and throws
    `ArgumentOutOfRangeException`; to use default rules, omit the options.
12. **Don't encode tie-breaks in the costs.** Skewing step costs (e.g. 1024 / 1025) to prefer straight lines and
    dividing costs back into steps is fragile and changes which routes are cheapest. Use `TieBreaking` and read `Steps` /
    `DiagonalSteps` instead.
13. **The cell type is `PathCell`, not `Cell`** (renamed in 2.0), so it does not clash with a game's own `Cell`.
    Code written for 1.x that says `Cell` means `PathCell`.

## Decision guide

- **Which constructor?** Have a 2D array → `PathCell[,]` (simplest). Have game objects → `ICellHolder[,]`.
  Have a pre-flattened row- or column-major array and want zero copy → `PathCell[]` (+ width/height; set
  `CellLayout` to say which).
- **Many short queries on a big `PathCell[,]`/holder grid?** Set `GridSnapshotMode.Manual` and update through
  `Set*` (or call `Refresh()` after direct edits).
- **Which query?** A route → `GetPath`. All reachable tiles within a budget → `GetReachable`.
  Just visibility/distance → `HasLineOfSight` / `Get*Distance`. A teaching animation → `BeginStepwiseSearch`.
  Different occupancy rules for one query, or a path onto an occupied cell → the same call with `QueryOptions`.
- **Caching?** Enable it when the same `(agent size, from, to)` queries repeat on a static grid; remember to
  `InvalidateCache()` after direct grid edits (`Set*` updates invalidate for you).

---

*This page mirrors the public API surface and is kept in sync with the source. If a signature here ever
disagrees with [`docs/api/`](api/README.md) or the code, trust the code. Maintainers: the upkeep rules
for this file live in [`CLAUDE.md`](../CLAUDE.md).*
