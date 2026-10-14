using BenchmarkDotNet.Attributes;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Benchmarks.Suites;

/// <summary>
/// Short queries on large open grids: a 2-step and a 6-step <see cref="Pathfinder.GetPath(Coordinate, Coordinate, int)"/>
/// and the matching <see cref="Pathfinder.GetReachable(Coordinate, int, int)"/> range from the grid's center, as a
/// tactics game runs them many times per turn. The search touches a handful of cells, so what is left is the
/// per-query overhead: with <see cref="GridSnapshotMode.PerQuery"/> the copy of the matrix into the snapshot, with
/// <see cref="GridSnapshotMode.Manual"/> nothing that scales with the grid.
/// </summary>
[MemoryDiagnoser]
public class ShortQueryBenchmarkRunner : IDisposable
{
    private Pathfinder _pathfinder;
    private Coordinate _origin;

    [Params(256, 1024)]
    public int GridSize { get; set; }

    [Params(GridSnapshotMode.PerQuery, GridSnapshotMode.Manual)]
    public GridSnapshotMode SnapshotMode { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var cells = new Cell[GridSize, GridSize];

        for (var x = 0; x < GridSize; x++)
        {
            for (var y = 0; y < GridSize; y++)
            {
                cells[x, y] = new Cell { IsWalkable = true };
            }
        }

        _pathfinder = new Pathfinder(cells, new PathfinderSettings { GridSnapshotMode = SnapshotMode });
        _origin = new Coordinate(GridSize / 2, GridSize / 2);
    }

    [Benchmark]
    public void GetPath_2Steps()
    {
        using var result = _pathfinder.GetPath(_origin, new Coordinate(_origin.X + 2, _origin.Y));
    }

    [Benchmark]
    public void GetPath_6Steps()
    {
        using var result = _pathfinder.GetPath(_origin, new Coordinate(_origin.X + 6, _origin.Y));
    }

    [Benchmark]
    public void GetReachable_2Steps()
    {
        using var result = _pathfinder.GetReachable(_origin, 200);
    }

    [Benchmark]
    public void GetReachable_6Steps()
    {
        using var result = _pathfinder.GetReachable(_origin, 600);
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _pathfinder?.Dispose();
        _pathfinder = null;
    }
}
