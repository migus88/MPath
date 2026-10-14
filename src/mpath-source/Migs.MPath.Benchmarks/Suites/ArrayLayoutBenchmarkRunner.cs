using BenchmarkDotNet.Attributes;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Benchmarks.Suites;

/// <summary>
/// A 2-step <see cref="Pathfinder.GetPath(Coordinate, Coordinate, int)"/> and <see cref="Pathfinder.GetReachable(Coordinate, int, int)"/>
/// near the corner of an open 256x256 grid, with the grid handed over as a <see cref="PathCell"/>[,] matrix or as a
/// <see cref="PathCell"/>[] array in column-major or row-major order (declared through <see cref="CellLayout"/>). The search touches
/// a handful of cells, so what is left is the per-query cost of preparing the grid.
/// </summary>
[MemoryDiagnoser]
public class ArrayLayoutBenchmarkRunner : IDisposable
{
    private const int GridSize = 256;
    private Pathfinder _pathfinder;

    public enum GridKind
    {
        Matrix,
        ArrayColumnMajor,
        ArrayRowMajor,
    }

    [Params(GridKind.Matrix, GridKind.ArrayColumnMajor, GridKind.ArrayRowMajor)]
    public GridKind Grid { get; set; }

    [Params(GridSnapshotMode.PerQuery, GridSnapshotMode.Manual)]
    public GridSnapshotMode SnapshotMode { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var settings = new PathfinderSettings
        {
            GridSnapshotMode = SnapshotMode,
            CellLayout = Grid == GridKind.ArrayRowMajor ? CellLayout.RowMajor : CellLayout.ColumnMajor
        };

        _pathfinder = Grid == GridKind.Matrix
            ? new Pathfinder(CreateMatrix(), settings)
            : new Pathfinder(CreateArray(Grid), GridSize, GridSize, settings);
    }

    [Benchmark]
    public void GetPath_2Steps()
    {
        using var result = _pathfinder.GetPath(new Coordinate(1, 1), new Coordinate(3, 1));
    }

    [Benchmark]
    public void GetReachable_2Steps()
    {
        using var result = _pathfinder.GetReachable(new Coordinate(1, 1), 200);
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _pathfinder?.Dispose();
        _pathfinder = null;
    }

    private static PathCell[,] CreateMatrix()
    {
        var cells = new PathCell[GridSize, GridSize];

        for (var x = 0; x < GridSize; x++)
        {
            for (var y = 0; y < GridSize; y++)
            {
                cells[x, y] = new PathCell { IsWalkable = true };
            }
        }

        return cells;
    }

    private static PathCell[] CreateArray(GridKind kind)
    {
        var cells = new PathCell[GridSize * GridSize];

        for (var x = 0; x < GridSize; x++)
        {
            for (var y = 0; y < GridSize; y++)
            {
                var index = kind == GridKind.ArrayRowMajor ? y * GridSize + x : x * GridSize + y;
                cells[index] = new PathCell { IsWalkable = true };
            }
        }

        return cells;
    }
}
