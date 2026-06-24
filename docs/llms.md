# MPath — AI Agent & LLM Reference

> **Purpose.** This is a single, self-contained, machine-first reference for AI coding agents (and the
> humans pairing with them) who need to *use* the MPath library in their own project. It front-loads the
> public API surface, the capabilities, the canonical usage patterns, and the non-obvious rules that cause
> bugs. Read this top-to-bottom and you can write correct MPath code without opening any other file.
>
> If you are instead modifying **this repository's own source**, read [`CLAUDE.md`](../CLAUDE.md) first —
> it documents the dual NuGet/Unity layout and where the source of truth lives.
>
> **Maintenance contract (read this if you change the public API).** This document mirrors the public API
> and must be updated in the *same change* that alters it. See [Maintenance contract](#maintenance-contract)
> at the bottom. A stale agent reference is worse than none — agents will confidently emit code against
> signatures that no longer exist.

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
| Coordinate origin | `(0,0)` is one corner; `X` and `Y` are non-negative grid indices, not world units. |

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
# .NET
dotnet add package Migs.MPath
```

```
# Unity (OpenUPM CLI, run in the project folder)
openupm add com.migsweb.mpath
```

---

## Mental model — four building blocks

1. **`Cell`** — one grid square. Public, mutable fields: `Coordinate`, `IsWalkable`, `IsOccupied`,
   `Weight`. *You* own the grid data; MPath copies/reads it.
2. **`IAgent`** — the thing moving. Single member `int Size` (in cells; `1` = occupies one tile,
   `> 1` triggers a clearance scan so the agent only fits where a `Size×Size` block is walkable).
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

// 1. Build a grid (matrix form is simplest). Cells default to walkable? No — set fields explicitly.
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

## Public API surface (complete)

### `Pathfinder` — `sealed`, `IDisposable`

**Constructors** (pick the one matching your grid representation; `settings` is optional):

```csharp
Pathfinder(Cell[]        cells,            int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
Pathfinder(ICellHolder[] holders,          int fieldWidth, int fieldHeight, IPathfinderSettings settings = null)
Pathfinder(Cell[,]       cellsMatrix,                                       IPathfinderSettings settings = null)
Pathfinder(ICellHolder[,] cellHoldersMatrix,                               IPathfinderSettings settings = null)
```

- Matrix constructors infer width/height from the array dimensions.
- The flat `Cell[]` constructor sorts the array **in place** (X-major) and is the only mode that does not
  copy — the other three flatten into a pooled internal buffer.
- `ICellHolder` lets you back cells with your own objects (e.g. Unity `MonoBehaviour`s) via
  `Cell CellData { get; }`.

**Methods:**

| Signature | Returns | Notes |
|-----------|---------|-------|
| `GetPath(IAgent agent, Coordinate from, Coordinate to)` | `PathResult` | Core A* query. Throws `ArgumentNullException` if `agent` is null, `ArgumentException` if `to` is outside the grid. Returns a **failure** result (not an exception) when no path exists. |
| `GetReachable(IAgent agent, Coordinate from, float budget)` | `RangeResult` | Every cell reachable for total cost `<= budget` (movement-range / "where can this unit move?"). Uniform-cost flood fill. |
| `HasLineOfSight(Coordinate from, Coordinate to, LineOfSightMode mode = BlockedByUnwalkableCells)` | `bool` | Single-cell Bresenham ray. Endpoints are not tested; agent size ignored. |
| `static GetManhattanDistance(Coordinate from, Coordinate to)` | `int` | `|dx| + |dy|`. Pure metric, no grid state, allocation-free. |
| `static GetChebyshevDistance(Coordinate from, Coordinate to)` | `int` | `max(|dx|, |dy|)`. Pure metric, allocation-free. |
| `BeginStepwiseSearch(IAgent agent, Coordinate from, Coordinate to)` | `StepwiseSearch` | Educational/visualization driver running the same A* one expansion at a time. **Pins the grid until disposed** — the pathfinder can't serve other queries meanwhile, and a second concurrent session throws. |
| `EnablePathCaching(IPathCaching handler = null)` | `Pathfinder` | Fluent (returns `this`). Opt-in. Pass your own `IPathCaching` or use the built-in `DefaultPathCaching`. |
| `DisablePathCaching()` | `Pathfinder` | Fluent. |
| `InvalidateCache()` | `Pathfinder` | Fluent. **Mutating the grid does NOT auto-invalidate** — call this yourself after the grid changes. |
| `Dispose()` | `void` | Returns the rented cell buffer to the pool. Call once, at end of life. |

### Result types

**`PathResult`** (`IDisposable`):
- `bool IsSuccess`
- `int Length` — number of steps.
- `IEnumerable<Coordinate> Path` — the steps, **excluding the origin** (`from`).
- `Coordinate Get(int index)`
- `Dispose()` — returns the pooled `Coordinate[]`.

**`RangeResult`** (`IDisposable`, from `GetReachable`):
- `bool IsSuccess` · `int Length`
- `IEnumerable<ReachableCell> Cells`
- `ReachableCell Get(int index)`
- `bool Contains(Coordinate coordinate)`
- `bool TryGetCost(Coordinate coordinate, out float cost)`
- `Dispose()`

**`ReachableCell`** (`readonly struct`): `Coordinate Coordinate`, `float Cost`.

**`StepwiseSearch`** (nested `Pathfinder.StepwiseSearch`, `IDisposable`):
- `SearchState State` · `bool IsComplete`
- `SearchStep Tick()` — advance one expansion; returns an immutable snapshot.
- `SearchStep RunToCompletion()` — tick until done, return the final snapshot.
- `Dispose()` — unpins the grid (mandatory).

**`SearchStep`** (plain immutable class — *not* disposable, owns plain arrays so snapshots survive):
`SearchState State`, `bool IsComplete`, `int Iteration`, `Coordinate Current`,
`IReadOnlyList<SearchNode> Searched`, `int OpenCount`, `int ClosedCount`,
`IReadOnlyList<Coordinate> Path` (raw, unsmoothed, origin-excluded).

**`SearchNode`** (`readonly struct`): `Coordinate Coordinate`, `SearchNodeState State`,
`float ScoreG`, `float ScoreH`, `float ScoreF`.

### Grid & geometry types

- **`Cell`** (`struct`): `Coordinate Coordinate`, `bool IsWalkable`, `bool IsOccupied`, `float Weight`,
  `void Reset()`. **All fields default to their zero value** — `IsWalkable` is `false` and `Weight` is
  `0` unless you set them. Initialize every cell explicitly.
- **`Coordinate`** (`struct`, `IEquatable`): `int X`, `int Y`, `bool IsInitialized`, `static Zero`,
  ctor `(int x, int y)`, operators `== != + -`, implicit `→ (int x, int y)`, explicit `(int,int) →`.

### Interfaces

- **`IAgent`**: `int Size { get; }`
- **`ICellHolder`**: `Cell CellData { get; }`
- **`IPathfinderSettings`**: the settings contract (see table below).
- **`IPathCaching : IDisposable`**: `bool TryGetCachedPath(IAgent, Coordinate from, Coordinate to, out PathResult)`,
  `void CachePath(IAgent, Coordinate from, Coordinate to, PathResult)`, `void ClearCache()`.

### Enums

| Enum | Values |
|------|--------|
| `LineOfSightMode` | `BlockedByUnwalkableCells` (0, default) · `IgnoreUnwalkableCells` (1) |
| `PathSmoothingMethod` | `None` (0) · `Simple` (1) · `StringPulling` (2) |
| `SearchState` | `InProgress` · `Success` · `Failure` |
| `SearchNodeState` | `Open` · `Closed` |

---

## Settings reference — `PathfinderSettings` (`: IPathfinderSettings`)

Construct with object-initializer syntax and pass to a `Pathfinder` constructor. Converted once into an
internal fast struct, so changing the instance after construction has **no effect** — build a new
`Pathfinder` to change settings.

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
Coordinate c = transform.position2D.ToCoordinate();   // from Vector2Int
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
3. **Not thread-safe.** No concurrent queries on one instance, no sharing across threads. One instance
   per thread.
4. **`Path` excludes the origin.** It is the sequence of steps *after* `from`. Don't expect `from` as the
   first element.
5. **No path → failure result, not an exception.** Check `IsSuccess`. Only bad arguments throw
   (`agent == null`; `to` outside the grid).
6. **Grid edits aren't seen automatically.** You mutate your own `Cell` data, but the pathfinder reads a
   flattened snapshot each query *and* may cache. After changing walkability/occupancy/weight, call
   `InvalidateCache()` if caching is on. (The pathfinder re-reads the grid each query, but cached results
   are returned verbatim.)
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

## Where things live (for agents editing this repo)

The library source of truth is **only** `src/mpath-unity-project/Packages/MPath/Source/`. The standalone
.NET project links those files via a glob — there is no second copy. Keep `Source/` free of
`UnityEngine`. Full details and build/test commands are in [`CLAUDE.md`](../CLAUDE.md).

- Algorithm: `Source/Pathfinder.cs` (+ `Pathfinder_PathSmoothing.cs`, `_Reachability.cs`, `_Geometry.cs`, `_Stepwise.cs`)
- Public data types & enums: `Source/Data/`
- Interfaces: `Source/Interfaces/`
- Caching: `Source/Caching/`
- Unity glue: `src/mpath-unity-project/Packages/MPath/Runtime/Code/`
- Human-facing docs: `docs/api/` (one file per type), `docs/guides/` (task guides)

---

## Maintenance contract

This file is **canonical AI-facing documentation** and must not drift from the code. Update it in the
**same commit/PR** that changes the public API. Specifically, when you:

- **add/rename/remove a public type, method, constructor, property, enum value, or setting** → update the
  matching section here (API surface, settings table, enums) *and* the per-type page under `docs/api/`;
- **change a default, a cost formula, or a lifecycle/disposal rule** → update [Settings reference](#settings-reference--pathfindersettings--ipathfindersettings) and/or [Critical rules & gotchas](#critical-rules--gotchas-the-bug-causers);
- **add a capability** → add a recipe under [Capabilities & recipes](#capabilities--recipes) and a row to the API table;
- **change namespaces, package ids, or the target framework** → update [At a glance](#at-a-glance) and [Namespaces](#namespaces-what-to-using).

Keep signatures copy-paste-correct: an agent reading this will emit code verbatim. When in doubt, verify
against `Source/` rather than memory. This contract is also recorded in `CLAUDE.md` so future agents keep
it in sync.
