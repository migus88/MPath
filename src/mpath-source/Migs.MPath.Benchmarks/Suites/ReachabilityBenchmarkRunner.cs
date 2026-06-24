using BenchmarkDotNet.Attributes;
using Migs.MPath.Benchmarks.Common;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using Migs.MPath.Tools;

namespace Migs.MPath.Benchmarks.Suites;

/// <summary>
/// Benchmarks <see cref="Pathfinder.GetReachable"/> (the movement-range / Dijkstra flood fill).
/// The work scales with the size of the reachable set, which is governed by the budget, so each
/// benchmark uses a different budget: a small disc, a mid-range area, and a budget large enough to
/// flood the entire connected region (worst case). With the default movement multipliers
/// (straight 1.0, diagonal 1.41) a budget value is roughly the travel distance in tiles.
/// </summary>
[MemoryDiagnoser]
public class ReachabilityBenchmarkRunner : IDisposable
{
    private readonly Coordinate _origin = BenchmarkScenario.Start;

    private readonly Pathfinder _pathfinder;
    private readonly IAgent _agent;

    public ReachabilityBenchmarkRunner()
    {
        var maze = new Maze(BenchmarkScenario.MazePath);
        _agent = new BenchmarkAgent();
        _pathfinder = new Pathfinder(maze.Cells);
    }

    [Benchmark]
    public void GetReachable_SmallBudget()
    {
        using var result = _pathfinder.GetReachable(_agent, _origin, 25f);
    }

    [Benchmark]
    public void GetReachable_MediumBudget()
    {
        using var result = _pathfinder.GetReachable(_agent, _origin, 100f);
    }

    [Benchmark]
    public void GetReachable_LargeBudget()
    {
        using var result = _pathfinder.GetReachable(_agent, _origin, 1000f);
    }

    public void Dispose()
    {
        _pathfinder?.Dispose();
    }
}
