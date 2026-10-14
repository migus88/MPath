using BenchmarkDotNet.Attributes;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Benchmarks.Suites;

/// <summary>
/// A 2-step <see cref="Pathfinder.GetPath(Coordinate, Coordinate, int)"/> and <see cref="Pathfinder.GetReachable(Coordinate, float, int)"/>
/// near the corner of an open 256x256 grid, with the grid handed over as a <see cref="Cell"/>[,] matrix or as a
/// <see cref="Cell"/>[] array in column-major, row-major or shuffled order, under default settings. The search touches
/// a handful of cells, so what is left is the per-query cost of preparing the grid.
/// </summary>
[MemoryDiagnoser]
public class ArrayLayoutBenchmarkRunner : IDisposable
{
    private const int GridSize = 256;
    private const int Seed = 1234;

    private Pathfinder _pathfinder;

    public enum GridKind
    {
        Matrix,
        ArrayColumnMajor,
        ArrayRowMajor,
        ArrayShuffled,
    }

    [Params(GridKind.Matrix, GridKind.ArrayColumnMajor, GridKind.ArrayRowMajor, GridKind.ArrayShuffled)]
    public GridKind Grid { get; set; }

    [Params(GridSnapshotMode.PerQuery, GridSnapshotMode.Manual)]
    public GridSnapshotMode SnapshotMode { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var settings = new PathfinderSettings { GridSnapshotMode = SnapshotMode };

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
        using var result = _pathfinder.GetReachable(new Coordinate(1, 1), 2f);
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _pathfinder?.Dispose();
        _pathfinder = null;
    }

    private static Cell[,] CreateMatrix()
    {
        var cells = new Cell[GridSize, GridSize];

        for (var x = 0; x < GridSize; x++)
        {
            for (var y = 0; y < GridSize; y++)
            {
                cells[x, y] = CreateCell(x, y);
            }
        }

        return cells;
    }

    private static Cell[] CreateArray(GridKind kind)
    {
        var cells = new Cell[GridSize * GridSize];

        for (var x = 0; x < GridSize; x++)
        {
            for (var y = 0; y < GridSize; y++)
            {
                var index = kind == GridKind.ArrayRowMajor ? y * GridSize + x : x * GridSize + y;
                cells[index] = CreateCell(x, y);
            }
        }

        if (kind == GridKind.ArrayShuffled)
        {
            var random = new Random(Seed);

            for (var i = cells.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
        }

        return cells;
    }

    private static Cell CreateCell(int x, int y)
    {
        return new Cell { Coordinate = new Coordinate(x, y), IsWalkable = true };
    }
}
