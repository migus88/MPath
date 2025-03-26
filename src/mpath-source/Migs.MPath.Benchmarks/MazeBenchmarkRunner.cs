using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Migs.MPath.Benchmarks.MazeBenchmarks;

namespace Migs.MPath.Benchmarks;

[MemoryDiagnoser(false)]
public class MazeBenchmarkRunner
{
    private readonly (int x, int y) _start = (10, 10);
    private readonly (int x, int y) _destination = (502, 374);
        
    private static readonly string AtomicRunner = nameof(MigsMazeBenchmarkRunner);
    private static readonly string RoyTRunner = nameof(RoyTAStarMazeBunchmarkRunner);

    private readonly Dictionary<string, IMazeBenchmarkRunner> _benchmarkRunners =
        new()
        {
            [AtomicRunner] = new MigsMazeBenchmarkRunner(),
            [RoyTRunner] = new RoyTAStarMazeBunchmarkRunner(),
        };
        
    public MazeBenchmarkRunner()
    {
        foreach (var benchmarkRunner in _benchmarkRunners.Values)
        {
            benchmarkRunner.Init(null);
        }
    }

    public void PrintAllResults()
    {
        foreach (var benchmarkRunner in _benchmarkRunners.Values)
        {
            benchmarkRunner.RenderPath(_start, _destination);
        }
    }

    [Benchmark] public void AtomicPathfinding() => _benchmarkRunners[AtomicRunner].FindPath(_start, _destination);
    [Benchmark] public void RoyTAStar() => _benchmarkRunners[RoyTRunner].FindPath(_start, _destination);
}