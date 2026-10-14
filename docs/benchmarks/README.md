# MPath Benchmarks

This document contains detailed information about MPath's performance benchmarks and how to run them yourself.

## Complex Maze Pathfinding

![Benchmark Maze](../../src/mpath-source/Migs.MPath.Benchmarks/Mazes/cavern.png)

*The complex maze used for benchmarking*

The following benchmark was run on a complex maze to test pathfinding performance. The test involved finding a path from coordinates (10, 10) to (502, 374) through a maze with narrow passages and dead ends. All benchmarks were implemented using [BenchmarkDotNet](https://benchmarkdotnet.org/).

### Implementation Comparison

| Method    | Mean      | Allocated   |
|---------- |----------:|------------:|
| MPath     |  4.223 ms |       178 B |
| [AStarLite](https://github.com/valantonini/AStar) |  8.042 ms |  8.75 MB |
| [RoyTAStar](https://github.com/roy-t/AStar) | 62.412 ms | 12.30 MB |
| [LinqToAStar](https://arc.net/l/quote/iqcsmlgc) | 5,884.7 ms | 108.13 MB |

MPath returns a cheapest route (its A* heuristic never overestimates the remaining cost). On this maze the narrow corridors leave little room for a better route; on open or randomly obstructed grids the exact cost model also cuts the number of expanded cells sharply.

These results highlight MPath's optimization for both speed and memory efficiency. Memory allocation in MPath is required only for the initial pathfinder creation and for the final path result creation, with no GC pressure during the pathfinding algorithm execution.

### 2.0 Compared With 1.2.1

These are the same queries on the same machine, with the 1.2.1 release and 2.0 benchmarked in alternation (1.2.1, 2.0, 1.2.1, 2.0). Each value is the mean of the two passes, and the two passes differed by under 3%. 2.0 uses its defaults (`PathCell[,]` matrix, `GridSnapshotMode.PerQuery`, `TieBreaking.None`), and 1.2.1's float budgets match 2.0's integer budgets (25 → 2500). Every result is disposed.

| Query | 1.2.1 | 2.0 | 2.0, `Manual` snapshot | Allocated (1.2.1 → 2.0) |
|-------|------:|----:|-----------------------:|------------------------:|
| Maze path (10,10) to (502,374) | 5.34 ms | 4.22 ms | 4.19 ms | 34–42 B → 50 B |
| Reachability on the maze, budget 25 | 1.018 ms | 39.1 µs | 11.6 µs | 34 B → 72 B |
| Reachability on the maze, budget 100 | 1.173 ms | 179.8 µs | 150.4 µs | 34 B → 72 B |
| Reachability on the maze, budget 1000 | 4.97 ms | 3.66 ms | 3.62 ms | 42 B → 77 B |
| 200x200, 20% random walls, corner to corner | 4.16 ms | 0.676 ms | | 34–42 B → 41 B |
| 500x500, 20% random walls, corner to corner | 29.74 ms | 4.23 ms | | 72 B → 50–58 B |
| 2-step path, 256x256 open grid | 330 µs | 9.2 µs | 0.21 µs | 33 B → 40 B |
| 2-step path, 1024x1024 open grid | 5,439 µs | 173 µs | 0.21 µs | 42 B → 40 B |
| `TryGetCost` on each of 2,040 reached cells | 1,614 µs | 17.9 µs | | 2 B → 0 B |

Most of the gain comes from three changes. A query no longer clears every cell of the grid, because search state is reset lazily per query. The open set is a fixed array heap. Range lookups are O(1).

Building a pathfinder now rents its search state (one node per cell, plus the heap) from `ArrayPool` and returns it on `Dispose`:

| Construction over the maze (196,608 cells) | 1.2.1 | 2.0 |
|------|------:|----:|
| `new Pathfinder(PathCell[,])`, disposed | 54 ns, 320 B | 46 µs, 312 B |
| `new Pathfinder(PathCell[,])`, not disposed | 41.6 µs, 14.0 MB | 424 µs, 10.0 MB |
| `new Pathfinder(PathCell[], w, h)`, not disposed | 38 ns, 320 B | 413 µs, 8.0 MB |

Build a pathfinder once per grid and dispose it, rather than once per query. 1.2.1's array constructor was nearly free because it searched the caller's array in place, keeping the search state in the cells and sorting the caller's array. 2.0 leaves the caller's array untouched.

### Path Smoothing Comparison

The following benchmark compares different path smoothing options using the same maze and coordinates:

| Method                           | Mean      | Allocated | Path Length |
|--------------------------------- |----------:|----------:|-----------:|
| NoSmoothing                      |  4.273 ms |      50 B | 1078 |
| SimpleSmoothing                  |  4.291 ms |      50 B | 318 |
| StringPullingSmoothing           |  6.229 ms |      50 B | 200 |
| StringPullingSmoothingLargeAgent | 10.486 ms |      60 B | 401 |

The results show that:
- Simple smoothing adds negligible overhead compared to no smoothing
- String pulling smoothing adds about 46% overhead and allocates no more than an unsmoothed query: the smoothed path goes into an array rented from `ArrayPool`, so a query whose result is disposed allocates only its result object (the 16 KB per query shown here before came from the benchmark not disposing its results)
- Path smoothing significantly reduces the number of steps in the path (SimpleSmoothing: 71% reduction, StringPullingSmoothing: 81% reduction)
- `StringPullingSmoothingLargeAgent` runs the same query with a 2x2 agent: every shortcut checks the agent's footprint at each cell, so both the search and the smoothing cost more

### Tie-Breaking Cost

`TieBreaking.None` (the default) runs the plain search. A tie-break runs a keyed search that also orders equal-cost routes (by diagonals or turns) and settles every route's cells before the cells after them, so it explores more on open ground with many equal-cost routes. Costs are the same `int`s in both. Same maze and machine, `PathCell[,]` grid:

| Query | None | FewerDiagonals | FewerTurns |
|-------|-----:|---------------:|-----------:|
| Maze path (10,10) to (502,374) | 4.25 ms | 5.14 ms | 5.33 ms |
| Reachability, budget 2500 | 0.039 ms | 0.042 ms | 0.042 ms |
| Reachability, budget 100000 | 3.68 ms | 4.03 ms | 4.03 ms |
| 200x200 open grid, unit costs, (5,5) to (190,120) | 0.33 ms | 0.83 ms | 0.75 ms |

No mode allocates during the search.

## Benchmark Environment Specs

```
BenchmarkDotNet v0.14.0, macOS 26.6.2 (25G83) [Darwin 25.6.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 7.0.20 (7.0.2024.26716), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 7.0.20 (7.0.2024.26716), Arm64 RyuJIT AdvSIMD
```

The 2.0 numbers are from the end of the 2.0 work. One unrelated single-threaded process used 35–50% of one core during the runs, and nothing else was busy.

## Measurement Legend

```
Mean      : Arithmetic mean of all measurements
Error     : Half of 99.9% confidence interval
StdDev    : Standard deviation of all measurements
Ratio     : Mean of the ratio distribution ([Current]/[Baseline])
Allocated : Allocated memory per single operation (managed only, inclusive, 1KB = 1024B)
1 ms      : 1 Millisecond (0.001 sec)
```

## Running Benchmarks

### .NET Benchmarks

The benchmark project is a small CLI (built with [Spectre.Console](https://spectreconsole.net/)).
Run it from the repository root and pass a command; add `--help` to see everything available.

```bash
# List all commands
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- --help

# Maze comparison vs. other A* libraries (default suite)
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark

# Other suites
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark smoothing
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark internal
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark reachability
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark grid
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark short
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark results
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark layout

# Render result images / print path stats
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- render
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- info smoothing
```

### Movement-Range (Reachability)

The `reachability` suite measures `Pathfinder.GetReachable` — the bounded Dijkstra flood fill that returns
every cell within a movement budget. Because the work scales with the size of the reachable set, the suite
sweeps a small, medium and large budget over the same maze. Allocation stays at a few dozen bytes per query
thanks to the pooled result buffer.

### Random Walls

The `grid` suite runs corner-to-corner `GetPath` queries over seeded grids with 20% random walls. These grids are
mostly open, so the search keeps a wide frontier and the open set does more of the work than on the maze.

| Method              | Mean      | Allocated |
|-------------------- |----------:|----------:|
| RandomWalls_200x200 | 0.676 ms  |      41 B |
| RandomWalls_500x500 | 4.227 ms  |   50–58 B |

Allocation figures of a few dozen to a few hundred bytes are the result object plus, amortized, the pooled result
array: BenchmarkDotNet forces a full GC between iterations, which can make `ArrayPool<T>.Shared` drop its arrays, so
the first query of an iteration rents a fresh one. Outside the harness a query allocates only its result object.

### Short Queries on Large Grids

The `short` suite runs a 2-step and a 6-step `GetPath` and `GetReachable` from the center of 256x256 and 1024x1024
open `PathCell[,]` grids, under both [`GridSnapshotMode`](../api/GridSnapshotMode.md)s. The search touches a handful of
cells, so the numbers are the per-query overhead: the copy of the matrix with `PerQuery`, nothing that scales with the
grid with `Manual`.

| Method              | Grid | PerQuery  | Manual   |
|-------------------- |-----:|----------:|---------:|
| GetPath_2Steps      |  256 | 9.3 µs    | 0.21 µs  |
| GetPath_6Steps      |  256 | 10.3 µs   | 0.52 µs  |
| GetReachable_2Steps |  256 | 12.2 µs   | 0.61 µs  |
| GetReachable_6Steps |  256 | 13.9 µs   | 4.9 µs   |
| GetPath_2Steps      | 1024 | 165 µs    | 0.21 µs  |
| GetPath_6Steps      | 1024 | 162 µs    | 0.52 µs  |
| GetReachable_2Steps | 1024 | 173 µs    | 0.61 µs  |
| GetReachable_6Steps | 1024 | 178 µs    | 4.9 µs   |

Results allocate 40 B (path) and 72 B (range). 1.2.1 had no snapshot mode and cleared the whole grid on every query: 0.33 ms on 256x256 and 5.4 ms on 1024x1024 for the same queries.

### Reading Results

The `results` suite measures reading results rather than computing them: enumerating the cross-maze `PathResult`
and a medium-budget `RangeResult` (`foreach` over the `Path` / `Cells` properties and over the result,
`AsSpan()` and `Get(i)`), `CopyCostsTo`, and an O(1) `TryGetCost` for every reached cell of the range.

The path has 1,078 cells and the range has 2,040 cells (budget 10000).

| Method                            | Mean     | Allocated | 1.2.1    |
|---------------------------------- |---------:|----------:|---------:|
| Path_ForeachPathProperty          | 0.75 µs  |         - | 5.84 µs  |
| Path_GetLoop                      | 4.15 µs  |         - | 5.73 µs  |
| Path_Foreach                      | 0.96 µs  |         - |          |
| Path_AsSpan                       | 0.67 µs  |         - |          |
| Range_ForeachCellsProperty        | 1.57 µs  |         - | 9.22 µs  |
| Range_Foreach                     | 2.47 µs  |         - |          |
| Range_CopyCostsTo                 | 13.9 µs  |         - |          |
| Range_TryGetCost_EveryReachedCell | 18.0 µs  |         - | 1,614 µs |

`foreach` over `Path` / `Cells`, over the result itself and over `AsSpan()` are all allocation-free: `Path` and `Cells` are
struct sequences that share the result's struct enumerator ([#71](https://github.com/migus88/MPath/issues/71); 1.2.1's
`yield` iterators allocated and were 8x / 6x slower). The two `*Property` rows were measured on the same machine after #71,
under load from other processes; the rest of the table is unchanged within noise. LINQ over `Path` / `Cells` boxes them.

### Cell Layout

The `layout` suite runs 2-step queries on a 256x256 open grid. The grid is given as a `PathCell[,]` matrix or as a `PathCell[]` array in each [`CellLayout`](../api/CellLayout.md). An array is searched in place and has no snapshot, so it costs the same in both modes.

| Grid             | Query        | PerQuery | Manual  |
|----------------- |------------- |---------:|--------:|
| Matrix           | GetPath      | 9.2 µs   | 0.21 µs |
| Matrix           | GetReachable | 10.5 µs  | 0.52 µs |
| ArrayColumnMajor | GetPath      | 0.21 µs  | 0.21 µs |
| ArrayColumnMajor | GetReachable | 0.52 µs  | 0.52 µs |
| ArrayRowMajor    | GetPath      | 0.21 µs  | 0.21 µs |
| ArrayRowMajor    | GetReachable | 0.52 µs  | 0.53 µs |

### Unity Benchmarks

The Unity project has the maze comparison as Editor performance tests (Unity Performance Testing package), in `src/mpath-unity-project/Assets/Benchmarks/Editor/PerformanceBenchmarks.cs`:

1. Open the Unity project at `src/mpath-unity-project`
2. Open **Window > General > Test Runner**, select **EditMode** and run the `PerformanceBenchmarks` tests (`RunAllPathfindingBenchmarks` runs every library on every maze in `Assets/Benchmarks/Mazes`; `MPathBenchmark`, `AStarLiteBenchmark` and `RoyTAStarBenchmark` run one library)
3. Open **Tools > Benchmarks > Results > View Results** to compare the results
