using BenchmarkDotNet.Attributes;
using Migs.MPath.Benchmarks.Common;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using Migs.MPath.Tools;

namespace Migs.MPath.Benchmarks.Suites;

/// <summary>
/// Micro-benchmarks for MPath internals: the cost of constructing a <see cref="Pathfinder"/> via the
/// different initialization modes, and short/long path searches with and without disposing the result.
/// </summary>
[MemoryDiagnoser]
public class InternalBenchmarkRunner : IDisposable
{
    private static readonly (int x, int y) ShortPathDestination = (10, 11);

    private readonly Maze _maze = new(BenchmarkScenario.MazePath);
    private readonly IAgent _agent = new BenchmarkAgent();
    private readonly Pathfinder _pathfinder;
    private readonly Cell[] _cellsArray;

    public InternalBenchmarkRunner()
    {
        _pathfinder = new Pathfinder(_maze.Cells);
        _cellsArray = _maze.Cells.Cast<Cell>().ToArray();
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

    public PathResult FindPath((int x, int y) start, (int x, int y) destination)
    {
        if (_pathfinder == null)
        {
            throw new Exception("Pathfinder is not initialized");
        }

        var result = _pathfinder.GetPath(_agent, (Coordinate)start, (Coordinate)destination);

        if (!result.IsSuccess)
        {
            throw new Exception("Path not found");
        }

        return result;
    }

    public void Dispose()
    {
        _pathfinder?.Dispose();
    }
}
