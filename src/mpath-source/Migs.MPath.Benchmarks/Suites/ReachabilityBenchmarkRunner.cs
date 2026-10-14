using BenchmarkDotNet.Attributes;
using Migs.MPath.Benchmarks.Common;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Tools;

namespace Migs.MPath.Benchmarks.Suites;

/// <summary>
/// Benchmarks <see cref="Pathfinder.GetReachable"/> (the movement-range / Dijkstra flood fill).
/// The work scales with the size of the reachable set, which is governed by the budget, so each
/// benchmark uses a different budget: a small disc, a mid-range area, and a budget large enough to
/// flood the entire connected region (worst case). With the default step costs
/// (straight 100, diagonal 141) a budget of 100 is roughly one tile of travel distance.
/// </summary>
[MemoryDiagnoser]
public class ReachabilityBenchmarkRunner : IDisposable
{
    private readonly Coordinate _origin = BenchmarkScenario.Start;

    private readonly Pathfinder _pathfinder;

    public ReachabilityBenchmarkRunner()
    {
        var maze = new Maze(BenchmarkScenario.MazePath);
        _pathfinder = new Pathfinder(maze.Cells);
    }

    [Benchmark]
    public void GetReachable_SmallBudget()
    {
        using var result = _pathfinder.GetReachable(_origin, 2500);
    }

    [Benchmark]
    public void GetReachable_MediumBudget()
    {
        using var result = _pathfinder.GetReachable(_origin, 10000);
    }

    [Benchmark]
    public void GetReachable_LargeBudget()
    {
        using var result = _pathfinder.GetReachable(_origin, 100000);
    }

    public void Dispose()
    {
        _pathfinder?.Dispose();
    }
}
