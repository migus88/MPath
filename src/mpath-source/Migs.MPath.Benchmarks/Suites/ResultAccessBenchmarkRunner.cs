using BenchmarkDotNet.Attributes;
using Migs.MPath.Benchmarks.Common;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Tools;

namespace Migs.MPath.Benchmarks.Suites;

/// <summary>
/// Reading results rather than computing them: walking the cross-maze <see cref="PathResult"/> and the
/// <see cref="RangeResult"/> of a medium budget, and looking up every reached cell's cost in the range (the
/// "is this cell in range, and what does it cost" query a tactics game runs for every cell it highlights).
/// The results are computed once in the setup, so the numbers are the access cost alone.
/// </summary>
[MemoryDiagnoser]
public class ResultAccessBenchmarkRunner : IDisposable
{
    private const int RangeBudget = 10000;

    private Pathfinder _pathfinder;
    private PathResult _path;
    private RangeResult _range;
    private Coordinate[] _reached;
    private int[] _costs;

    [GlobalSetup]
    public void Setup()
    {
        var maze = new Maze(BenchmarkScenario.MazePath);
        _pathfinder = new Pathfinder(maze.Cells);
        _path = _pathfinder.GetPath(BenchmarkScenario.Start, BenchmarkScenario.Destination);
        _range = _pathfinder.GetReachable(BenchmarkScenario.Start, RangeBudget);
        _reached = new Coordinate[_range.Length];
        _costs = new int[maze.Cells.Length];

        for (var i = 0; i < _range.Length; i++)
        {
            _reached[i] = _range.Get(i).Coordinate;
        }
    }

    [Benchmark]
    public int Path_ForeachPathProperty()
    {
        var sum = 0;

        foreach (var coordinate in _path.Path)
        {
            sum += coordinate.X;
        }

        return sum;
    }

    [Benchmark]
    public int Path_GetLoop()
    {
        var sum = 0;

        for (var i = 0; i < _path.Length; i++)
        {
            sum += _path.Get(i).X;
        }

        return sum;
    }

    [Benchmark]
    public int Path_Foreach()
    {
        var sum = 0;

        foreach (var coordinate in _path)
        {
            sum += coordinate.X;
        }

        return sum;
    }

    [Benchmark]
    public int Path_AsSpan()
    {
        var sum = 0;

        foreach (var coordinate in _path.AsSpan())
        {
            sum += coordinate.X;
        }

        return sum;
    }

    [Benchmark]
    public long Range_ForeachCellsProperty()
    {
        var sum = 0L;

        foreach (var cell in _range.Cells)
        {
            sum += cell.Cost;
        }

        return sum;
    }

    [Benchmark]
    public long Range_Foreach()
    {
        var sum = 0L;

        foreach (var cell in _range)
        {
            sum += cell.Cost;
        }

        return sum;
    }

    [Benchmark]
    public int Range_CopyCostsTo()
    {
        return _range.CopyCostsTo(_costs);
    }

    [Benchmark]
    public long Range_TryGetCost_EveryReachedCell()
    {
        var sum = 0L;

        for (var i = 0; i < _reached.Length; i++)
        {
            _range.TryGetCost(_reached[i], out var cost);
            sum += cost;
        }

        return sum;
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _path?.Dispose();
        _range?.Dispose();
        _pathfinder?.Dispose();
        _path = null;
        _range = null;
        _pathfinder = null;
    }
}
