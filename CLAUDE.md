# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

MPath is a high-performance A* pathfinding library for 2D grids, shipped as **both** a NuGet package (`Migs.MPath`) and a Unity package (`com.migsweb.mpath`). It targets `netstandard2.1`, uses `unsafe` code and `ArrayPool` to minimize GC allocations, and is **not thread-safe**.

## Critical: source of truth and the dual-project layout

The actual library source lives **only** in the Unity package:

```
src/mpath-unity-project/Packages/MPath/Source/   <- THE library source (edit here)
src/mpath-unity-project/Packages/MPath/Runtime/  <- Unity-only glue (ScriptableObject settings, extensions)
src/mpath-source/                                 <- standalone .NET solution (tests, benchmarks, NuGet packaging)
```

The standalone `.NET` project **does not contain its own copy** of the source. `Migs.MPath.Core.csproj` pulls it in via a linked glob:

```xml
<Compile Include="..\..\mpath-unity-project\Packages\MPath\Source\**\*.cs">
```

**Implication: always edit core library code under `src/mpath-unity-project/Packages/MPath/Source/`.** Both the NuGet build and the Unity package consume those same files. There is no separate copy to keep in sync. (The `Migs.MPath.Core/obj/` folders contain only generated `AssemblyInfo`, not real source.)

The `Runtime/Code/` folder (namespace `Migs.MPath`, asmdef `Migs.MPath.Runtime`) is the **Unity-only** layer — it references `UnityEngine` and is excluded from the NuGet package. `Source/` (asmdef `Migs.MPath.Core`, `noEngineReferences: true`) must stay engine-agnostic so it compiles for plain .NET.

## Common commands

All commands assume repo root. The solution is `src/mpath-source/Migs.MPath.sln` (projects: Core, Tools, Tests, Benchmarks). Requires .NET 7 SDK (tests target `net7.0`; the library targets `netstandard2.1`).

```bash
# Build everything (Debug)
dotnet build src/mpath-source/Migs.MPath.sln

# Run all tests
dotnet test src/mpath-source/Migs.MPath.Tests/Migs.MPath.Tests.csproj

# Run a single test by name (NUnit + FluentAssertions + NSubstitute)
dotnet test src/mpath-source/Migs.MPath.Tests/Migs.MPath.Tests.csproj --filter "FullyQualifiedName~PathfinderTests.GetPath_WithValidPath_ReturnsSuccess"
# or match a fixture / partial name
dotnet test src/mpath-source/Migs.MPath.Tests/Migs.MPath.Tests.csproj --filter "Name~Smoothing"
```

CI (`.github/workflows/run-tests.yml`) runs restore → build → test on PRs to `master` using the same Tests project.

### Benchmarks

The Benchmarks project is a BenchmarkDotNet console app with a **Spectre.Console.Cli** front end (commands live in `Commands/`, dispatched from a thin `Program.cs`). Running with no command — or `--help` — prints usage instead of throwing.

```bash
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark               # maze comparison vs other A* libs (default)
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark smoothing      # path-smoothing benchmark
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark internal       # internal micro-benchmarks
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark reachability    # GetReachable movement-range flood fill
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark grid            # corner-to-corner GetPath over 200x200 / 500x500 grids with 20% random walls
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark short           # 2-/6-step GetPath and GetReachable on 256x256 / 1024x1024 open grids, per GridSnapshotMode
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark results         # reading results: enumeration, AsSpan, O(1) range lookups, CopyCostsTo
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark layout          # 2-step queries on a 256x256 grid as a matrix or a column-/row-major/shuffled Cell[], per GridSnapshotMode
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- render                    # render result images (SkiaSharp)
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- info smoothing            # print smoothed path lengths
```

Project layout: `Suites/` holds the BenchmarkDotNet `[Benchmark]` classes, `Competitors/` holds the rival-library adapters (`IMazeBenchmarkRunner`) compared in the maze suite, `Commands/` holds the CLI commands, and `Common/` holds the shared `BenchmarkScenario` (maze path + canonical start/destination, all resolved against the assembly base directory so commands work from any working directory). `Migs.MPath.Tools` (uses SkiaSharp) provides the maze loading/rendering used by tests and benchmarks.

## Release pipeline

Releases are driven by interactive bash scripts in `ci/` (macOS-oriented; uses BSD `sed` and a hardcoded Unity Hub path). Do not invoke these casually — they tag and build artifacts.

- `ci/release.sh` — the orchestrator: checks clean `master`, optionally bumps version, builds Release, runs tests, copies the `.nupkg` to `builds/`, exports the Unity package, and creates a **local** git tag (push is manual).
- `ci/version-increment.sh [major|minor|patch]` — the single source for version bumps. It edits **two files together**, which must always stay in lockstep: `src/mpath-source/Migs.MPath.Core/Migs.MPath.Core.csproj` (`<Version>` and `<AssemblyVersion>`) and `src/mpath-unity-project/Packages/MPath/package.json` (`version`). The README version badge is updated manually.
- `ci/process-unity-package.sh` — runs Unity in batch mode (`Migs.MPath.Editor.PackageExporter.ExportPackageFromBatchMode`, defined in `src/mpath-unity-project/Assets/Editor/PackageExporter.cs`) to export a `.unitypackage`, then rewrites each asset's `pathname` from `Assets/...` to `Packages/...` so the package installs into the consumer's Packages folder.
- `ci/publish-nuget.sh` — pushes the latest `builds/*.nupkg` to NuGet.org and/or GitHub Packages (`NUGET_API_KEY` / `GITHUB_TOKEN`).

## Architecture

### Core algorithm — `Source/Pathfinder.cs` (+ `Pathfinder_PathSmoothing.cs`, `Pathfinder_Reachability.cs`, `Pathfinder_Geometry.cs`, `Pathfinder_Stepwise.cs`, `Pathfinder_TieBreaking.cs`)

`Pathfinder` is a `sealed unsafe partial class`. The hot path operates on a flat `Cell[]` via a raw `Cell*` pointer (`fixed`/`stackalloc`), indexed by `GetLayoutIndex` (`x * _xStride + y * _yStride`: column-major, or row-major for a declared/detected row-major `Cell[]`; see `GetCell`). Key performance design points to preserve when editing:

- **Search state lives in the pathfinder, reset lazily.** `Cell` holds only user data (16 bytes); the search state is a pooled `PathNode[]` (`Internal/PathNode.cs`, 24 bytes: `ScoreG`, `ScoreH`, `ParentIndex`, `Depth`, `QueueIndex`, `Generation`) indexed like `_cells`. A search pins both buffers and calls `BeginSearch`, which bumps `_generation` (clearing the nodes once on `uint` wrap-around) and resets the open set; nothing walks the grid. A node is part of the running search only when its `Generation` matches: untouched → `OpenNode` initialises and queues it; touched → closed (`QueueIndex == PathNode.ClosedQueueIndex`) or open (decrease-key). This relies on "touched ⇒ queued at least once" — touch a node only when enqueuing it. Searches never write into `Cell`s. `SearchGeneration` (internal) lets tests force the wrap-around.
- **`ArrayPool<Cell>.Shared`** backs every constructor except the raw `Cell[]` one, which rents a snapshot only for an unordered array (the node and heap buffers are pooled in all modes). `Dispose()` (idempotent, guarded by `_isDisposed`; every public instance method starts with `ThrowIfDisposed()`, so add it to new ones) returns the rented arrays — so `Pathfinder` is `IDisposable` and meant to be a **reused, long-lived instance**, while `PathResult` (which rents a `Coordinate[]` from a pool) is **per-query and must be disposed** (`using`). Disposing results, not recreating the pathfinder, is the intended usage.
- **Four constructors → `InitializationMode`** (`CellsArray`, `CellHoldersArray`, `CellsMatrix`, `CellHoldersMatrix`). `InitializeCellsArray()` prepares `_cells` before each search: `TakeSnapshot` copies the caller's representation into the pooled snapshot at `GetLayoutIndex(coordinate)`, skipped when `GridSnapshotMode.Manual` is set and `_isSnapshotCurrent` (cleared by the public `Refresh()`, which also clears the path cache; the matrix-mode and unordered-array cell setters write the snapshot too, so they need no refresh). **`Cell[]` mode never sorts or writes the caller's array** (`_sourceCells`): `ResolveArrayLayout` runs at the first query and after `Refresh()` (eagerly in the constructor for a declared `Unordered`), detects (`CellLayout.Auto`) or checks (declared `ColumnMajor`/`RowMajor`, `InvalidOperationException` on mismatch) the layout in one O(N) pass and sets the strides; a row/column-major array is `_cells` itself (searched in place, no copy), an unordered one gets a pooled column-major snapshot (`_arraySnapshot`) plus `_arraySourceIndexes` (snapshot index → caller index, for O(1) setters/`GetIndex`), and from then on behaves like matrix mode. Snapshots are always column-major; only a `Cell[]` searched in place can be row-major. Every write into `_cells` and every pointer lookup must go through `GetLayoutIndex`, or neighbor adjacency silently transposes.
- **`stackalloc Cell*[8]` neighbors**, populated cardinal-first then diagonal. Diagonal inclusion respects `IsDiagonalMovementEnabled` and the corner rule: the cardinal expansion classifies each flank as `CellBlocker` Open/Wall/Occupied (`GetFootprintBlocker`, footprint-wide for `Size > 1`, wall wins), and `FastPathfinderSettings.IsCornerPassable` looks the pair up in a 9-bit table built once from `CornerBlocking`. Agent `Size > 1` triggers a clearance scan in `GetWalkableLocation`.
- **`GetPath` is optimal.** Costs are `int` everywhere (no floats, no hidden scale). Step cost is `GetStepCost(direction, neighbor)` (`StraightStepCost`/`DiagonalStepCost` **+** the entered cell's `Weight` when `IsCellWeightEnabled`, the weight clamped to `[0, FastPathfinderSettings.MaxCost]`), shared with `GetReachable`, so a route's cost means the same in both. The heuristic (`GetH`) is `major * max(dx,dy) + minor * min(dx,dy)` with coefficients computed once in `FastPathfinderSettings` (octile with diagonals, Manhattan without, scaled by the step costs, diagonal capped at two straight steps): it is admissible and consistent, which A* needs because a closed cell is never reopened. Overflow: `MaxCost = int.MaxValue / 2` is the headroom; every relaxation skips a step with `stepCost > MaxCost - g` (reachability: `min(budget, MaxCost) - g`), and `FastPathfinderSettings.Validate` (constructor) requires step costs ≥ 1 and `(Width + Height) * larger step cost ≤ MaxCost`, so `h ≤ MaxCost` and `g + h` never overflows. A cheaper `g` for an open cell goes through `_openSet.UpdatePriority` (decrease-key). `PathfinderOptimalityTests` checks both queries against an independent reference Dijkstra on seeded random grids; keep it green when touching costs, the heuristic or the open set.
- **Two cost modes, fixed per pathfinder by `TieBreaking`.** `None` (default) is the plain search above (`CalculatePath` calls `ExpandNextPlain` directly, so its loop has no mode check). Any tie-break switches `GetPath`/`GetReachable`/stepwise to the keyed search in `Pathfinder_TieBreaking.cs` (`CalculatePathKeyed`, `ExpandNextKeyed`, `ProcessNeighborsKeyed`, `CalculateReachableKeyed`, `WalkRoute`): costs are the same ints (`PathNode.ScoreG`, same `GetStepCost`/`GetH`); each node holds a lexicographic label (`ScoreG`, `TieKey` = diagonals or turns, plus `Depth` for fewer diagonals) and `_arrivals[index]`, a bitmask of the directions that reach it with that label; the route is walked back from those masks (keep direction, else first in `DirectionIndexes` order) instead of from `ParentIndex`. `PathNode` is `LayoutKind.Explicit`: both searches keep the cost in `ScoreG`, `TieKey` overlays `ParentIndex`, and the plain reachability's `DiagonalSteps` overlays `ScoreH` (unused there), so it stays 24 bytes. Only a tie-breaking pathfinder rents `_arrivals` (1 byte/cell) and a `KeyedPriorityQueue` (16-byte entries ordered by priority f, tie key, then cost g ascending); the plain `UnsafePriorityQueue` is untouched. The cost-ascending last key makes every equal-label predecessor leave the queue before its successor, so the masks are complete and the route never depends on heap order (preferring larger g on f ties breaks exact fewest-turns — `PathfinderOptimalityTests` catches it). Fewest turns stays exact with one label per node because (min turns, all arrival directions achieving it) is a sufficient statistic. Keep both searches: the keyed one costs about 3x on open ground, so `None` stays the fast default.
- **One A* iteration = `ExpandNext`** (dequeue → goal check → close + `PopulateNeighbors`/`ProcessNeighbors`). `CalculatePath`'s main loop and the stepwise search both call it, so the two share the exact same expansion logic (and find the same path). It is `AggressiveInlining` to keep the batch hot path unchanged — preserve that if you touch it.

### Geometry helpers — `Source/Pathfinder_Geometry.cs`

Public spatial queries that don't produce a path: `static GetManhattanDistance`/`GetChebyshevDistance` (pure integer metrics over `Coordinate`, no grid state) and the instance `HasLineOfSight(from, to, LineOfSightMode mode = BlockedByUnwalkableCells)`. LOS pins `_cells` and delegates to the private Bresenham `HasLineOfSight(from, to, Cell*, mode)` in `Pathfinder_PathSmoothing.cs`. String-pulling smoothing does **not** use it: it validates shortcuts with `IsSegmentTraversable`, an 8-connected Bresenham walk that checks the agent's `Size x Size` footprint (`GetWalkableLocation`) at every step and the corner rule at every diagonal step (`IsDiagonalStepAllowed`, which mirrors `PopulateNeighbors`; keep the two in sync when the corner rule changes). The per-cell test is `IsLineOfSightBlocked`: under `BlockedByUnwalkableCells` a non-walkable cell blocks; under `IgnoreUnwalkableCells` it's transparent (see-through terrain). In **both** modes an occupied cell blocks when `IsCalculatingOccupiedCells` is set — the mode governs walkability only, occupancy stays orthogonal. Endpoints are never tested; the ray is single-cell (agent size ignored). `LineOfSightMode` is a public enum in `Source/Data/`.

### Stepwise search — `Source/Pathfinder_Stepwise.cs`

An **educational/visualization** API that runs the same A* one expansion at a time. `Pathfinder.BeginStepwiseSearch(agent, from, to)` returns a `Pathfinder.StepwiseSearch` (a public nested `IDisposable` class) you advance with `Tick()`; each tick returns an immutable `SearchStep` snapshot of the accumulated searched area (open + closed cells, with A* scores) plus the path once it succeeds. Chosen over the user-suggested "subclass Pathfinder" because `Pathfinder` is `sealed` and the algorithm internals are `private` — a nested class reuses them all (`ExpandNext`, `_openSet`, `BeginSearch`, `OpenNode`, `GetCell`, …) **without** duplicating logic or widening access modifiers. Key points: it **pins `_cells` and `_nodes` via `GCHandle`s** for the whole search (the search holds raw pointers into both across ticks that must stay valid), so the session **must be disposed** and the owning `Pathfinder` can't service other queries until then (an `_activeSearch` reference throws on a second concurrent session, and `Pathfinder.Dispose` disposes the active session before returning the buffer it pins). Discovery is tracked incrementally via a `bool[] _seen` keyed by pointer offset; per-tick snapshot building is O(discovered) — deliberately not optimized, since this is not the hot path. The visualized `Path` is the **raw** parent-pointer path (no smoothing), matching `PathResult`'s origin-excluded convention.

### Open set — `Source/Internal/UnsafePriorityQueue.cs`

A binary min-heap of `HeapEntry` (`int Priority` + `int Index`, the cell/node index) in a fixed array of `Size + 1` entries (1-based), rented from `ArrayPool` in the constructor and returned by `Pathfinder.Dispose`. A cell is queued at most once, so the heap never grows and no search allocates for it. Its API takes and returns node indexes. Sifts compare the entries' priorities and never read the nodes they pass; they only write each moved node's `PathNode.QueueIndex`, which gives O(1) `Contains` (the slot must be live and hold that node, so a stale `QueueIndex` needs no reset) and O(log n) `UpdatePriority`, the real decrease-key used by A* and the reachability search. Every search calls `Reset(nodes)` (via `BeginSearch`) with the pinned node pointer before the first `Enqueue`. Hot-path null checks are `Debug.Assert`s. Measured and rejected: a separate positions array instead of the node's `QueueIndex` (~4% slower) and a 4-ary heap (no gain, slower on the maze); `UnsafePriorityQueueTests` checks the heap against a `SortedSet` reference and `PathfinderAllocationTests` pins zero search allocations.

### Data types — `Source/Data/`

- `Cell` (struct, 16 bytes): only public user fields (`Coordinate`, `IsWalkable`, `IsOccupied`, `Weight`). Search state is in `PathNode` (see above).
- `GridSnapshotMode` (enum: `PerQuery` default, `Manual`) and `CellLayout` (enum: `Auto` default, `ColumnMajor`, `RowMajor`, `Unordered`), threaded `IPathfinderSettings` → `FastPathfinderSettings` → `InitializeCellsArray`/`ResolveArrayLayout`.
- `Coordinate` (`readonly struct`, 8 bytes: `X`, `Y`; `default` equals `Zero`, no initialized flag; `CoordinateTests` pins the size of it and of `Cell`), `PathResult` (`IDisposable`; `IsSuccess`, `Length`, `Get(int)` and a `Path` enumerable over the pooled array), `PathfinderSettings` (public, mutable, implements `IPathfinderSettings`), `PathSmoothingMethod` (enum: `None`, etc.).
- Both results expose `AsSpan()`, an indexer and a public struct `Enumerator` (`foreach` over the result allocates nothing; `Path`/`Cells` are the old allocating iterators, kept for compatibility).
- `RangeResult` (`IDisposable`; result of `Pathfinder.GetReachable`, rents a `ReachableCell[]` from the pool — must be disposed). Pathfinder results are built with the internal `RangeResult.Create(cells, length, gridSize, strideX, strideY)` via `CreateRangeResult`, whose strides come from `GetLayoutIndex`, so `CopyCostsTo` follows any layout change automatically. `Contains`/`TryGetCost`/`TryGetCell` are O(1): the first lookup lazily rents an `int[]` sized to the reached cells' bounding box (capped at the grid; hand-made `Create` results fall back to a linear scan when sparse) and fills cell → result index; it is never cleared — an entry counts only if it is a valid index whose cell has that coordinate. The index is owned by the result (returned on `Dispose`), deliberately not read from the pathfinder's generation-stamped nodes, which the next query would overwrite and `ReachableCell` (readonly struct: `Coordinate` + `Cost` + integer `Steps`/`DiagonalSteps`). `PathResult.Steps`/`DiagonalSteps` count the route before smoothing (`CreatePathResult` sets them for both modes). The reachability search is a budget-bounded uniform-cost (Dijkstra) flood fill in `Pathfinder_Reachability.cs`; per-step cost = straight/diagonal step cost **+** cell `Weight` (added when weighting is enabled; the same `GetStepCost` the main A* uses), independent of the A* heuristic. Weight must be added, not multiplied — `Cell.Weight` defaults to 0, so a multiplier would zero out every step cost and flood the whole board.
- `SearchStep` (plain class, **not** pooled/disposable — it owns plain arrays so snapshots survive across ticks), `SearchNode` (readonly struct: `Coordinate` + `SearchNodeState` + `ScoreG/H/F`), `SearchState` (enum: `InProgress`/`Success`/`Failure`), `SearchNodeState` (enum: `Open`/`Closed`). These back the stepwise search (see above); they intentionally favour clarity over allocation since the stepwise API is not the hot path.

### Settings flow

Public `IPathfinderSettings` / `PathfinderSettings` (or Unity's `ScriptablePathfinderSettings` ScriptableObject) is converted **once** in the constructor into the internal readonly struct `FastPathfinderSettings` (`FromSettings`). The algorithm reads only the fast struct in the hot loop — when adding a setting, thread it through `IPathfinderSettings` → `FastPathfinderSettings` → the relevant `Pathfinder` method.

Per-query overrides (`QueryOptions`, a public `readonly ref struct`: `OccupancyRule` + `AlwaysEnterable` span + `IsDestinationAlwaysEnterable`) never touch `_settings` or the grid. The options overloads resolve them into the internal `QueryRules` struct held in the mutable `_query` field (the constructor sets it from the settings; an options query pins the span with `fixed`, sets `_query`, runs the plain query and restores the defaults in `finally`; `StepwiseSearch` copies the span and re-applies its rules around every tick). `GetBlocker` is the only reader: it takes occupancy from `_query` and scans the always-enterable cells only for an occupied cell, so the default path costs nothing extra. The corner table is unchanged, since options only reclassify a cell from Occupied to Open. Queries whose rules differ from the settings skip the path cache (`IsCachingQuery`, used by the private `TryGetCachedPath`/`CachePath`). Keep the options independent of where search state lives (it is not stored in `Cell`).

### Agent size

Each query (`GetPath`, `GetReachable`, `BeginStepwiseSearch`, each with and without `QueryOptions`) has an `IAgent` overload and an `int agentSize = 1` overload (size last, after the options). Both validate (`ArgumentNullException` / `ArgumentOutOfRangeException` for `agentSize < 1` via `ThrowIfInvalidAgentSize`; an `IAgent.Size < 1` is deliberately not validated, for compatibility) and forward to one private body (`FindPath`, `FindReachable`, the private `BeginStepwiseSearch`) that works on the `int` only. The algorithm never sees `IAgent`. The only consumer of the agent object is the path cache: `FindPath` carries the caller's agent (or `null`) and the cache helpers substitute `Internal/SizedAgent.Of(size)` (shared per size, equal by size) only when caching is on, so neither form allocates.

### Caching — `Source/Caching/` + `Source/Interfaces/IPathCaching.cs`

Opt-in via `EnablePathCaching()`. `GetPath` consults the cache before searching and stores successful results after, both through the private `TryGetCachedPath`/`CachePath` helpers in `Pathfinder.cs` (the single gate for any query that must bypass the cache). Ownership: the cache gets a private `PathResult.Copy()` on store and a hit is returned as a fresh copy, so every result `GetPath` returns is the caller's to dispose and the cache's instances are never handed out. `DefaultPathCaching` is the built-in implementation; callers can supply their own `IPathCaching`. `InvalidateCache()` clears it. The cell-update API (`Pathfinder_CellUpdates.cs`: `SetWalkable`/`SetOccupied`/`SetWeight`/`SetCell`, plus `GetCell`/`GetIndex`) clears it on a change cached paths depend on; direct writes to the caller's grid do **not** auto-invalidate; `Refresh()` ("the grid changed") clears it too.

### Cell updates — `Source/Pathfinder_CellUpdates.cs`

Setters write the caller's `Cell[]` (`_sourceCells`, found by `FindArrayIndex`: `GetLayoutIndex`, or `_arraySourceIndexes` for an unordered array, verified by coordinate, falling back to a linear search; never sorts) or `Cell[,]` (at `[X, Y]`, linear fallback), and in matrix mode and for an unordered array also the pooled snapshot. `GetIndex` returns the caller's array index (`FindArrayIndex`) for an unordered array, otherwise `GetLayoutIndex`. Holder modes throw `NotSupportedException` (holders are re-read every query). `GetLayoutIndex` in `Pathfinder.cs` is the single source of the layout (`GetCell(ptr, x, y)` uses it); if the layout changes, the init methods and these setters follow it.

### Unity integration — `Runtime/Code/`

`PathfindingExtensions` and `ScriptablePathfinderSettings` (a `ScriptableObject` form of the settings). Unity scene/example code lives in `src/mpath-unity-project/Assets/Examples/` (`Battlefield`, `Player`, `FieldCell` implementing `ICellHolder`) — these are sample/demo code, not part of either shipped package.

## Conventions

- The library uses `unsafe`/pointers deliberately for speed; `AllowUnsafeBlocks` is on in Core, Tests, and Tools. Keep `Source/` free of `UnityEngine` references.
- No public member carries `[Obsolete]` (`PublicApiTests` checks the whole assembly; the compiler's own marker on ref structs is exempt). Breaking changes happen in a major version and are listed in `docs/upgrading.md`, not kept as deprecated shims.
- Hot-path methods are annotated `[MethodImpl(MethodImplOptions.AggressiveInlining)]` — follow suit for new per-cell/per-neighbor helpers.
- Any change a caller could notice, and every breaking change, updates its section of `docs/upgrading.md` in the same PR.
- Detailed end-user docs live in `docs/` (`docs/api/` mirrors public types, `docs/guides/` covers usage); update them when changing public API surface.
- `docs/llms.md` is the **customer-facing AI-agent / LLM reference** — a single, machine-first page for agents that *use* the library (this `CLAUDE.md` is the counterpart for agents *editing* this repo). It mirrors the public API surface, capabilities, usage recipes, and the disposal/thread-safety/lifecycle rules so an agent can write correct MPath code without reading the whole docs tree. Keep it **purely consumer-facing** — no repo-layout or maintenance content (that all lives here). It is **canonical and must be kept in sync**: in the **same change** that alters a public type, method, constructor, property, enum value, setting, default, cost formula, or lifecycle/disposal rule, update `docs/llms.md` *and* the matching `docs/api/` page. Specifically:
  - new/renamed/removed type → update the **type map** (one row, linking to `docs/api/`) and/or the `Pathfinder` method table; do **not** add a full per-member dump — exhaustive signatures stay in `docs/api/` so this file stays lean and growth stays capped at ~one row per type;
  - changed default / cost formula / lifecycle rule → update the **Settings reference** and/or **Critical rules & gotchas**;
  - new capability → add a recipe under **Capabilities & recipes**.

  Keep signatures copy-paste-correct — agents emit them verbatim; verify against `Source/`, not memory.
