# MPath — AI Agent & LLM Reference

> **Audience: AI coding agents (and their humans) that want to *use* MPath in their own project.**
> This is a single, self-contained, machine-first page: the public API surface, the capabilities, the
> canonical usage patterns, and the non-obvious rules that cause bugs. Read it top-to-bottom and you can
> write correct MPath code without opening any other file. For full per-member signatures of a type, follow
> its link into [`docs/api/`](api/README.md).
>
> *(Modifying MPath's own source instead of using it? This page is not for you — see [`CLAUDE.md`](../CLAUDE.md).)*

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
| Grid indexing | Flat layout is `index = x * Height + y` (X-major). |
| Coordinate origin | `(0,0)` is one corner; `X`/`Y` are non-negative grid indices, not world units. |

## Namespaces (what to `using`)

```csharp
using Migs.MPath.Core;            // Pathfinder
using Migs.MPath.Core.Data;       // Cell, Coordinate, PathResult, RangeResult, ReachableCell,
                                  // PathfinderSettings, LineOfSightMode, PathSmoothingMethod,
                                  // SearchStep, SearchNode, SearchState, SearchNodeState
using Migs.MPath.Core.Interfaces; // IAgent, ICellHolder, IPathfinderSettings, IPathCaching
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

1. **`Cell`** — one grid square. Public, mutable fields: `Coordinate`, `IsWalkable`, `IsOccupied`,
   `Weight`. *You* own the grid data; MPath reads it.
2. **`IAgent`** — the thing moving. Single member `int Size` (in cells; `1` = one tile, `> 1` triggers a
   clearance scan so the agent only fits where a `Size×Size` block is walkable).
3. **`Pathfinder`** — the engine. Build it once from your grid + settings, reuse it for every query,
   dispose it at the end of its life.
4. **A result type per query** — `PathResult`, `RangeResult`, or `StepwiseSearch`. All are `IDisposable`
   and **must** be disposed (they rent pooled arrays / pin memory). Always `using`.

`Coordinate` is a value type: `new Coordinate(x, y)`, with `==`/`!=`/`+`/`-` operators and implicit
conversion to `(int x, int y)` (Unity callers can use `vector2Int.ToCoordinate()` /
`coordinate.ToVector2Int()`).

---

## The 30-second example

```csharp
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;

class Unit : IAgent { public int Size => 1; }

// 1. Build a grid (matrix form is simplest). Cell fields default to ZERO — set them explicitly.
var grid = new Cell[10, 10];
for (int x = 0; x < 10; x++)
for (int y = 0; y < 10; y++)
    grid[x, y] = new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true, Weight = 1f };

grid[3, 3].IsWalkable = false; // a wall

// 2. Create the pathfinder ONCE and reuse it. Settings are optional (sensible defaults).
using var pathfinder = new Pathfinder(grid);
var agent = new Unit();

// 3. Query. Dispose every result.
using var result = pathfinder.GetPath(agent, from: new Coordinate(0, 0), to: new Coordinate(9, 9));

if (result.IsSuccess)
{
    // result.Path does NOT include the origin; it is the sequence of steps after `from`.
    foreach (Coordinate step in result.Path) { /* move to step */ }
}
```

---

## Public API surface

Full per-member signatures for every type live in [`docs/api/`](api/README.md) (one page per type). This
section gives the entry point in full and maps the rest, so it stays small as the API grows.

### `Pathfinder` — the entry point (`sealed`, `IDisposable`)

**Constructors** (pick the one matching your grid representation; `settings` is optional):

```csharp
Pathfinder(Cell[]         cells,             int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
Pathfinder(ICellHolder[]  holders,           int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
Pathfinder(Cell[,]        cellsMatrix,                                        IPathfinderSettings settings = null)
Pathfinder(ICellHolder[,] cellHoldersMatrix,                                 IPathfinderSettings settings = null)
```

Matrix constructors infer width/height from the array. The flat `Cell[]` form sorts in place (X-major) and
is the only mode that doesn't copy; the others flatten into a pooled buffer. `ICellHolder` lets you back
cells with your own objects (e.g. Unity `MonoBehaviour`s) via `Cell CellData { get; }`.

**Methods:**

| Signature | Returns | Notes |
|-----------|---------|-------|
| `GetPath(IAgent agent, Coordinate from, Coordinate to)` | `PathResult` | Core A* query. Throws `ArgumentNullException` if `agent` is null, `ArgumentException` if `to` is outside the grid. Returns a **failure** result (not an exception) when no path exists. |
| `GetReachable(IAgent agent, Coordinate from, float budget)` | `RangeResult` | Every cell reachable for total cost `<= budget` ("where can this unit move?"). Uniform-cost flood fill. |
| `HasLineOfSight(Coordinate from, Coordinate to, LineOfSightMode mode = BlockedByUnwalkableCells)` | `bool` | Single-cell Bresenham ray. Endpoints not tested; agent size ignored. |
| `static GetManhattanDistance(Coordinate from, Coordinate to)` | `int` | `|dx| + |dy|`. Pure metric, allocation-free. |
| `static GetChebyshevDistance(Coordinate from, Coordinate to)` | `int` | `max(|dx|, |dy|)`. Pure metric, allocation-free. |
| `BeginStepwiseSearch(IAgent agent, Coordinate from, Coordinate to)` | `StepwiseSearch` | Educational/visualization driver running the same A* one expansion at a time. **Pins the grid until disposed** — the pathfinder can't serve other queries meanwhile, and a second concurrent session throws. |
| `EnablePathCaching(IPathCaching handler = null)` | `Pathfinder` | Fluent (returns `this`). Opt-in. Pass your own `IPathCaching` or use the built-in `DefaultPathCaching`. |
| `DisablePathCaching()` | `Pathfinder` | Fluent. |
| `InvalidateCache()` | `Pathfinder` | Fluent. **Mutating the grid does NOT auto-invalidate** — call this after the grid changes. |
| `Dispose()` | `void` | Returns the rented cell buffer to the pool. Call once, at end of life. |

### Everything else — type map

One row per type; click through for full members. The load-bearing facts are repeated in
[Capabilities & recipes](#capabilities--recipes) and [Critical rules & gotchas](#critical-rules--gotchas-the-bug-causers).

| Type | Kind | Gist | Details |
|------|------|------|---------|
| `PathResult` | result, `IDisposable` | `IsSuccess`, `Length`, `Path` (steps, **origin excluded**), `Get(i)` | [api](api/PathResult.md) |
| `RangeResult` | result, `IDisposable` | `IsSuccess`, `Length`, `Cells`, `Contains(c)`, `TryGetCost(c, out cost)`, `Get(i)` | [api](api/RangeResult.md) |
| `ReachableCell` | `readonly struct` | `Coordinate` + `Cost` | [api](api/ReachableCell.md) |
| `StepwiseSearch` | session, `IDisposable` | `Tick()`, `RunToCompletion()`, `State`, `IsComplete` | [api](api/StepwiseSearch.md) |
| `SearchStep` | immutable class | Per-tick snapshot: `Searched`, `Open/ClosedCount`, `Current`, `Iteration`, `Path` | [api](api/SearchStep.md) |
| `SearchNode` | `readonly struct` | `Coordinate` + `State` + `ScoreG/ScoreH/ScoreF` | [api](api/SearchNode.md) |
| `Cell` | `struct` | Grid square: `Coordinate`, `IsWalkable`, `IsOccupied`, `Weight` — **all default to zero** | [api](api/Cell.md) |
| `Coordinate` | `struct` | `X`, `Y`, `IsInitialized`, `static Zero`; ctor `(x,y)`; `== != + -`; `↔ (int,int)` | [api](api/Coordinate.md) |
| `IAgent` | interface | `int Size` (cells; `> 1` ⇒ clearance scan) | [api](api/IAgent.md) |
| `ICellHolder` | interface | `Cell CellData` — back cells with your own objects | [api](api/ICellHolder.md) |
| `IPathfinderSettings` · `PathfinderSettings` | settings | See [Settings reference](#settings-reference) below | [api](api/PathfinderSettings.md) |
| `IPathCaching` · `DefaultPathCaching` | caching | `TryGetCachedPath` / `CachePath` / `ClearCache` | [api](api/IPathCaching.md) |

### Enums

| Enum | Values |
|------|--------|
| `LineOfSightMode` | `BlockedByUnwalkableCells` (0, default) · `IgnoreUnwalkableCells` (1) |
| `PathSmoothingMethod` | `None` (0) · `Simple` (1) · `StringPulling` (2) |
| `SearchState` | `InProgress` · `Success` · `Failure` |
| `SearchNodeState` | `Open` · `Closed` |

---

## Settings reference

`PathfinderSettings` (`: IPathfinderSettings`). Construct with object-initializer syntax and pass to a
`Pathfinder` constructor. Converted once into an internal fast struct, so changing the instance after
construction has **no effect** — build a new `Pathfinder` to change settings.

| Property | Type | Default | Meaning |
|----------|------|---------|---------|
| `IsDiagonalMovementEnabled` | `bool` | `true` | Allow 8-direction movement. |
| `IsCalculatingOccupiedCells` | `bool` | `true` | Treat `IsOccupied` cells as blocked (for paths *and* line of sight). |
| `IsMovementBetweenCornersEnabled` | `bool` | `false` | Allow cutting around the corner of an obstacle diagonally. |
| `IsCellWeightEnabled` | `bool` | `true` | Add per-cell `Weight` to movement cost (terrain cost). |
| `StraightMovementMultiplier` | `float` | `1f` | Cost of a cardinal step. |
| `DiagonalMovementMultiplier` | `float` | `1.41f` | Cost of a diagonal step (≈√2). |
| `PathSmoothingMethod` | `PathSmoothingMethod` | `None` | Post-process the path (`Simple` / `StringPulling`). |
| `InitialBufferSize` | `int?` | `null` | Pre-size the result buffer to avoid growth on large paths. |

> **Weight is additive, not multiplicative.** With `IsCellWeightEnabled`, step cost = movement multiplier
> **+** destination cell `Weight`. Because `Weight` defaults to `0`, a flat default grid behaves like
> uniform cost. Set higher `Weight` for slow terrain (mud, hills).

---

## Capabilities & recipes

### Find a path

```csharp
using var result = pathfinder.GetPath(agent, from, to);
if (result.IsSuccess)
    foreach (var step in result.Path) { /* ... */ }   // origin excluded
```

### Movement range ("where can this unit move?", tactics games)

```csharp
using var range = pathfinder.GetReachable(agent, origin, budget: 5f);
foreach (var c in range.Cells)
    Highlight(c.Coordinate, c.Cost);                  // every cell with cheapest cost <= 5
if (range.TryGetCost(target, out var cost)) { /* reachable for `cost` */ }
```

Per-step cost mirrors the main A*: straight/diagonal multiplier **+** cell `Weight` (when weighting is on).

### Distance & line of sight (range checks, visibility — no path computed)

```csharp
int dist = Pathfinder.GetManhattanDistance(a, b);     // or GetChebyshevDistance — static, allocation-free
bool clearShot = pathfinder.HasLineOfSight(shooter, target);
// See-through terrain that blocks movement but not vision (water/glass/pit):
bool visible = pathfinder.HasLineOfSight(shooter, target, LineOfSightMode.IgnoreUnwalkableCells);
```

Occupancy is orthogonal to the mode: with `IsCalculatingOccupiedCells`, an occupied cell blocks sight in
*both* modes (so "ignore terrain, but units still block" is expressible).

### Visualize the search (teaching/demos only — use `GetPath` in production)

```csharp
using var search = pathfinder.BeginStepwiseSearch(agent, from, to);
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
pathfinder.EnablePathCaching();                       // fluent; reuses identical (agent, from, to) results
// ... after you mutate the grid:
pathfinder.InvalidateCache();                         // NOT automatic
```

### Multi-cell agents & terrain cost

- `Size > 1`: the agent only traverses cells where the whole `Size×Size` block is walkable.
- Set `Cell.Weight` for terrain cost and keep `IsCellWeightEnabled = true`.

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
   Dispose it once when you're done with it (or its grid).
3. **Not thread-safe.** No concurrent queries on one instance, no sharing across threads. One per thread.
4. **`Path` excludes the origin.** It is the sequence of steps *after* `from`. Don't expect `from` first.
5. **No path → failure result, not an exception.** Check `IsSuccess`. Only bad arguments throw
   (`agent == null`; `to` outside the grid).
6. **Grid edits aren't seen automatically by the cache.** The pathfinder re-reads your grid each query,
   but cached results are returned verbatim — call `InvalidateCache()` after changing the grid if caching
   is on.
7. **Settings are frozen at construction.** Mutating the `PathfinderSettings` object afterward does
   nothing — create a new `Pathfinder`.
8. **Indexing is `x * Height + y` (X-major).** Relevant only if you build the flat `Cell[]` form
   yourself; matrix/holder constructors handle it for you.
9. **`Cell` fields default to zero.** `IsWalkable` defaults to `false` — initialize every cell or the
   whole grid is unwalkable.
10. **Stepwise search is exclusive.** While a `StepwiseSearch` is alive, the owning pathfinder throws on
    any other query. Dispose it promptly.

## Decision guide

- **Which constructor?** Have a 2D array → `Cell[,]` (simplest). Have game objects → `ICellHolder[,]`.
  Have a pre-flattened array and want zero copy → `Cell[]` (+ width/height).
- **Which query?** A route → `GetPath`. All reachable tiles within a budget → `GetReachable`.
  Just visibility/distance → `HasLineOfSight` / `Get*Distance`. A teaching animation → `BeginStepwiseSearch`.
- **Caching?** Enable it when the same `(agent, from, to)` queries repeat on a static grid; remember to
  `InvalidateCache()` on grid changes.

---

*This page mirrors the public API surface and is kept in sync with the source. If a signature here ever
disagrees with [`docs/api/`](api/README.md) or the code, trust the code. Maintainers: the upkeep rules
for this file live in [`CLAUDE.md`](../CLAUDE.md).*
