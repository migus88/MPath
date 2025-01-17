using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Migs.Pathfinding.Core;
using Migs.Pathfinding.Core.Data;
using Migs.Pathfinding.Core.Interfaces;
using Migs.Pathfinding.Tools;

namespace Migs.Pathfinding.Benchmarks;

[MemoryDiagnoser]
public class InternalBenchmarkRunner : IDisposable
{
    private readonly Maze _maze = new("cavern.gif");
    private readonly IAgent _agent = new Agent();
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
        using var result = FindPath((10, 10), (502, 374));
    }

    [Benchmark]
    public void FindShortPath_WithUsing()
    {
        using var result = FindPath((10, 10), (10, 11));
    }

    [Benchmark]
    public void MatrixInitialization_NoUsing()
    {
        var pathfinder = new Pathfinder(_maze.Cells);
    }

    [Benchmark]
    public void FindLongPath_NoUsing()
    {
        var result = FindPath((10, 10), (502, 374));
    }

    [Benchmark]
    public void FindShortPath_NoUsing()
    {
        var result = FindPath((10, 10), (10, 11));
    }
    
    public PathResult FindPath((int x, int y) start, (int x, int y) destination)
    {
        if (_pathfinder == null)
        {
            throw new Exception("Pathfinder is not initialized");
        }
        
        var result = _pathfinder.GetPath(_agent, (Coordinate)start, (Coordinate)destination);

        if (!result.IsPathFound)
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