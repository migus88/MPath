using BenchmarkDotNet.Attributes;
using Migs.MPath.Benchmarks.Common;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Tools;

namespace Migs.MPath.Benchmarks.Suites;

/// <summary>
/// Micro-benchmarks for MPath internals: the cost of constructing a <see cref="Pathfinder"/> via the
/// different initialization modes (over the maze and over a 100x100 open grid), and short/long path searches with and without disposing the result, and a failing search towards a wall.
/// </summary>
[MemoryDiagnoser]
public class InternalBenchmarkRunner : IDisposable
{
    private const int SmallGridSize = 100;

    private static readonly (int x, int y) ShortPathDestination = (10, 11);

    private readonly Maze _maze = new(BenchmarkScenario.MazePath);
    private readonly Pathfinder _pathfinder;
    private readonly PathCell[] _cellsArray;
    private readonly PathCell[,] _smallCells = CreateOpenGrid(SmallGridSize);
    private readonly Coordinate _wallDestination;

    public InternalBenchmarkRunner()
    {
        _pathfinder = new Pathfinder(_maze.Cells);
        _cellsArray = _maze.Cells.Cast<PathCell>().ToArray();
        var wall = Array.FindIndex(_cellsArray, cell => !cell.IsWalkable);
        _wallDestination = new Coordinate(wall / _maze.Height, wall % _maze.Height);
    }

    [Benchmark]
    public void ArrayInitialization()
    {
        var pathfinder = new Pathfinder(_cellsArray, _maze.Width, _maze.Height);
    }

    [Benchmark]
    public void MatrixInitialization_WithUsing()
    {
        using var pathfinder = new Pathfinder(_maze.Cells);
    }

    [Benchmark]
    public void SmallMatrixInitialization_WithUsing()
    {
        using var pathfinder = new Pathfinder(_smallCells);
    }

    [Benchmark]
    public void SmallMatrixInitialization_NoUsing()
    {
        var pathfinder = new Pathfinder(_smallCells);
    }

    [Benchmark]
    public void FindLongPath_WithUsing()
    {
        using var result = FindPath(BenchmarkScenario.Start, BenchmarkScenario.Destination);
    }

    [Benchmark]
    public void FindShortPath_WithUsing()
    {
        using var result = FindPath(BenchmarkScenario.Start, ShortPathDestination);
    }

    [Benchmark]
    public void MatrixInitialization_NoUsing()
    {
        var pathfinder = new Pathfinder(_maze.Cells);
    }

    [Benchmark]
    public void FindLongPath_NoUsing()
    {
        var result = FindPath(BenchmarkScenario.Start, BenchmarkScenario.Destination);
    }

    [Benchmark]
    public void FindShortPath_NoUsing()
    {
        var result = FindPath(BenchmarkScenario.Start, ShortPathDestination);
    }

    [Benchmark]
    public void FindPath_ToUnwalkableDestination()
    {
        using var result = _pathfinder.GetPath(BenchmarkScenario.Start, _wallDestination);

        if (result.IsSuccess)
        {
            throw new Exception("Path to a wall must fail");
        }
    }

    public PathResult FindPath((int x, int y) start, (int x, int y) destination)
    {
        if (_pathfinder == null)
        {
            throw new Exception("Pathfinder is not initialized");
        }

        var result = _pathfinder.GetPath((Coordinate)start, (Coordinate)destination);

        if (!result.IsSuccess)
        {
            throw new Exception("Path not found");
        }

        return result;
    }

    private static PathCell[,] CreateOpenGrid(int size)
    {
        var cells = new PathCell[size, size];

        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                cells[x, y] = new PathCell { IsWalkable = true };
            }
        }

        return cells;
    }

    public void Dispose()
    {
        _pathfinder?.Dispose();
    }
}
