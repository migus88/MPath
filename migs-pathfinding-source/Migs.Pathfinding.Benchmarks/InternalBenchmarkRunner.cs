using BenchmarkDotNet.Attributes;
using Migs.Pathfinding.Core;
using Migs.Pathfinding.Core.Data;
using Migs.Pathfinding.Core.Interfaces;
using Migs.Pathfinding.Tools;

namespace Migs.Pathfinding.Benchmarks;

[MemoryDiagnoser]
public class InternalBenchmarkRunner
{
    private readonly Maze _maze = new("cavern.gif");
    private readonly IAgent _agent = new Agent();
    private Pathfinder _pathfinder;

    public InternalBenchmarkRunner()
    {
        _pathfinder = new Pathfinder(_maze.Cells);
    }

    [Benchmark]
    public void MatrixInitialization()
    {
        var pathfinder = new Pathfinder(_maze.Cells);
    }

    [Benchmark]
    public void PathFinding() => FindPath((10, 10), (502, 374));
    
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
}