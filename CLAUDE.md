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
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- render                    # render result images (SkiaSharp)
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- info smoothing            # print smoothed path lengths
```

Project layout: `Suites/` holds the BenchmarkDotNet `[Benchmark]` classes, `Competitors/` holds the rival-library adapters (`IMazeBenchmarkRunner`) compared in the maze suite, `Commands/` holds the CLI commands, and `Common/` holds the shared `BenchmarkAgent` and `BenchmarkScenario` (maze path + canonical start/destination, all resolved against the assembly base directory so commands work from any working directory). `Migs.MPath.Tools` (uses SkiaSharp) provides the maze loading/rendering used by tests and benchmarks.

## Release pipeline

Releases are driven by interactive bash scripts in `ci/` (macOS-oriented; uses BSD `sed` and a hardcoded Unity Hub path). Do not invoke these casually — they tag and build artifacts.

- `ci/release.sh` — the orchestrator: checks clean `master`, optionally bumps version, builds Release, runs tests, copies the `.nupkg` to `builds/`, exports the Unity package, and creates a **local** git tag (push is manual).
- `ci/version-increment.sh [major|minor|patch]` — the single source for version bumps. It edits **two files together**, which must always stay in lockstep: `src/mpath-source/Migs.MPath.Core/Migs.MPath.Core.csproj` (`<Version>` and `<AssemblyVersion>`) and `src/mpath-unity-project/Packages/MPath/package.json` (`version`). The README version badge is updated manually.
- `ci/process-unity-package.sh` — runs Unity in batch mode (`Migs.MPath.Editor.PackageExporter.ExportPackageFromBatchMode`, defined in `src/mpath-unity-project/Assets/Editor/PackageExporter.cs`) to export a `.unitypackage`, then rewrites each asset's `pathname` from `Assets/...` to `Packages/...` so the package installs into the consumer's Packages folder.
- `ci/publish-nuget.sh` — pushes the latest `builds/*.nupkg` to NuGet.org and/or GitHub Packages (`NUGET_API_KEY` / `GITHUB_TOKEN`).

## Architecture

### Core algorithm — `Source/Pathfinder.cs` (+ `Pathfinder_PathSmoothing.cs`, `Pathfinder_Reachability.cs`, `Pathfinder_Geometry.cs`, `Pathfinder_Stepwise.cs`)

`Pathfinder` is a `sealed unsafe partial class`. The hot path operates on a flat `Cell[]` via a raw `Cell*` pointer (`fixed`/`stackalloc`), indexed as `x * Height + y` (see `GetCell`). Key performance design points to preserve when editing:

- **Cells are reused, not reallocated.** `GetPath` pins the cell array, calls `ResetCells`, clears the open set, and runs A*. The same buffer serves every query.
- **`ArrayPool<Cell>.Shared`** backs every constructor except the raw `Cell[]` one. `Dispose()` returns the rented array — so `Pathfinder` is `IDisposable` and meant to be a **reused, long-lived instance**, while `PathResult` (which rents a `Coordinate[]` from a pool) is **per-query and must be disposed** (`using`). Disposing results, not recreating the pathfinder, is the intended usage.
- **Four constructors → `InitializationMode`** (`CellsArray`, `CellHoldersArray`, `CellsMatrix`, `CellHoldersMatrix`). `InitializeCellsArray()` dispatches to the matching `TryInitializing...` method to flatten the caller's representation into `_cells` before each search. The `Cell[]`-array mode sorts in place (`Utils.CellsComparison`) and is the only mode that does **not** pool/copy. `CellsComparison` sorts **X-major then Y-minor** so the sorted layout matches `GetCell`'s `x * Height + y` indexing (the matrix/holder modes write cells at `Coordinate.X * Height + Coordinate.Y`); keep all four modes consistent or neighbor adjacency silently transposes.
- **`stackalloc Cell*[8]` neighbors**, populated cardinal-first then diagonal. Diagonal inclusion respects `IsDiagonalMovementEnabled` and `IsMovementBetweenCornersEnabled` (corner-cutting). Agent `Size > 1` triggers a clearance scan in `GetWalkableLocation`.
- Heuristic is **Manhattan distance** (`GetH`). G-score folds in per-cell `Weight` (when `IsCellWeightEnabled`) and straight-vs-diagonal travel multipliers.
- **One A* iteration = `ExpandNext`** (dequeue → goal check → close + `PopulateNeighbors`/`ProcessNeighbors`). `CalculatePath`'s main loop and the stepwise search both call it, so the two share the exact same expansion logic (and find the same path). It is `AggressiveInlining` to keep the batch hot path unchanged — preserve that if you touch it.

### Geometry helpers — `Source/Pathfinder_Geometry.cs`

Public spatial queries that don't produce a path: `static GetManhattanDistance`/`GetChebyshevDistance` (pure integer metrics over `Coordinate`, no grid state) and the instance `HasLineOfSight(from, to, LineOfSightMode mode = BlockedByUnwalkableCells)`. LOS pins `_cells` and delegates to the private Bresenham `HasLineOfSight(from, to, Cell*, mode)` in `Pathfinder_PathSmoothing.cs` (also used by string-pulling smoothing, which always passes `BlockedByUnwalkableCells`). The per-cell test is `IsLineOfSightBlocked`: under `BlockedByUnwalkableCells` a non-walkable cell blocks; under `IgnoreUnwalkableCells` it's transparent (see-through terrain). In **both** modes an occupied cell blocks when `IsCalculatingOccupiedCells` is set — the mode governs walkability only, occupancy stays orthogonal. Endpoints are never tested; the ray is single-cell (agent size ignored). `LineOfSightMode` is a public enum in `Source/Data/`.

### Stepwise search — `Source/Pathfinder_Stepwise.cs`

An **educational/visualization** API that runs the same A* one expansion at a time. `Pathfinder.BeginStepwiseSearch(agent, from, to)` returns a `Pathfinder.StepwiseSearch` (a public nested `IDisposable` class) you advance with `Tick()`; each tick returns an immutable `SearchStep` snapshot of the accumulated searched area (open + closed cells, with A* scores) plus the path once it succeeds. Chosen over the user-suggested "subclass Pathfinder" because `Pathfinder` is `sealed` and the algorithm internals are `private` — a nested class reuses them all (`ExpandNext`, `_openSet`, `ResetCells`, `GetCell`, …) **without** duplicating logic or widening access modifiers. Key points: it **pins `_cells` via a `GCHandle`** for the whole search (the open set holds raw pointers across ticks that must stay valid), so the session **must be disposed** and the owning `Pathfinder` can't service other queries until then (a `_searchSessionActive` guard throws on a second concurrent session). Discovery is tracked incrementally via a `bool[] _seen` keyed by pointer offset; per-tick snapshot building is O(discovered) — deliberately not optimized, since this is not the hot path. The visualized `Path` is the **raw** parent-pointer path (no smoothing), matching `PathResult`'s origin-excluded convention.

### Open set — `Source/Internal/UnsafePriorityQueue.cs`

A custom binary-heap min-priority-queue over `Cell*`. Each `Cell` stores its own `QueueIndex` (internal field) so membership tests (`Contains`) and decrease-key updates are O(1)/O(log n) without a separate map. `UpdatePriority` performs a real decrease-key (re-heapifies via cascade up/down) and is used by the reachability search; the A* hot path mutates scores in place instead.

### Data types — `Source/Data/`

- `Cell` (struct): public fields (`Coordinate`, `IsWalkable`, `IsOccupied`, `Weight`) + `internal` algorithm scratch fields (`ScoreF/G/H`, `Depth`, `ParentCoordinate`, `QueueIndex`, `IsClosed`). `Reset()` clears only the scratch state.
- `Coordinate` (struct), `PathResult` (`IDisposable`; `IsSuccess`, `Length`, `Get(int)` and a `Path` enumerable over the pooled array), `PathfinderSettings` (public, mutable, implements `IPathfinderSettings`), `PathSmoothingMethod` (enum: `None`, etc.).
- `RangeResult` (`IDisposable`; result of `Pathfinder.GetReachable`, rents a `ReachableCell[]` from the pool — must be disposed) and `ReachableCell` (readonly struct: `Coordinate` + `Cost`). The reachability search is a budget-bounded uniform-cost (Dijkstra) flood fill in `Pathfinder_Reachability.cs`; per-step cost = straight/diagonal multiplier **+** cell `Weight` (added when weighting is enabled, matching the main A*'s additive `GetCellWeightMultiplier` term), independent of the A* heuristic. Weight must be added, not multiplied — `Cell.Weight` defaults to 0, so a multiplier would zero out every step cost and flood the whole board.
- `SearchStep` (plain class, **not** pooled/disposable — it owns plain arrays so snapshots survive across ticks), `SearchNode` (readonly struct: `Coordinate` + `SearchNodeState` + `ScoreG/H/F`), `SearchState` (enum: `InProgress`/`Success`/`Failure`), `SearchNodeState` (enum: `Open`/`Closed`). These back the stepwise search (see above); they intentionally favour clarity over allocation since the stepwise API is not the hot path.

### Settings flow

Public `IPathfinderSettings` / `PathfinderSettings` (or Unity's `ScriptablePathfinderSettings` ScriptableObject) is converted **once** in the constructor into the internal readonly struct `FastPathfinderSettings` (`FromSettings`). The algorithm reads only the fast struct in the hot loop — when adding a setting, thread it through `IPathfinderSettings` → `FastPathfinderSettings` → the relevant `Pathfinder` method.

### Caching — `Source/Caching/` + `Source/Interfaces/IPathCaching.cs`

Opt-in via `EnablePathCaching()`. `GetPath` consults the cache before searching and stores successful results after. `DefaultPathCaching` is the built-in implementation; callers can supply their own `IPathCaching`. `InvalidateCache()` clears it (the grid changing does **not** auto-invalidate).

### Unity integration — `Runtime/Code/`

`PathfindingExtensions` and `ScriptablePathfinderSettings` (a `ScriptableObject` form of the settings). Unity scene/example code lives in `src/mpath-unity-project/Assets/Examples/` (`Battlefield`, `Player`, `FieldCell` implementing `ICellHolder`) — these are sample/demo code, not part of either shipped package.

## Conventions

- The library uses `unsafe`/pointers deliberately for speed; `AllowUnsafeBlocks` is on in Core, Tests, and Tools. Keep `Source/` free of `UnityEngine` references.
- Hot-path methods are annotated `[MethodImpl(MethodImplOptions.AggressiveInlining)]` — follow suit for new per-cell/per-neighbor helpers.
- Detailed end-user docs live in `docs/` (`docs/api/` mirrors public types, `docs/guides/` covers usage); update them when changing public API surface.
