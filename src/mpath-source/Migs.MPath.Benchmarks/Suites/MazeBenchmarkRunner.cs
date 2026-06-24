using BenchmarkDotNet.Attributes;
using Migs.MPath.Benchmarks.Common;
using Migs.MPath.Benchmarks.Competitors;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Benchmarks.Suites;

/// <summary>
/// Head-to-head maze pathfinding benchmark: MPath against other A* libraries over the same maze and
/// the canonical <see cref="BenchmarkScenario"/> start/destination. Note that LinqToAStar is orders of
/// magnitude slower than the rest, so a full run takes a while.
/// </summary>
[MemoryDiagnoser(false)]
public class MazeBenchmarkRunner
{
    private readonly Coordinate _start = BenchmarkScenario.Start;
    private readonly Coordinate _destination = BenchmarkScenario.Destination;

    private static readonly string MPathRunner = nameof(MPathMazeBenchmarkRunner);
    private static readonly string RoyTRunner = nameof(RoyTAStarMazeBenchmarkRunner);
    private static readonly string AStarLiteRunner = nameof(AStarLiteBenchmarkRunner);
    private static readonly string LinqToAStarRunner = nameof(LinqToAStarMazeBenchmarkRunner);

    private readonly Dictionary<string, IMazeBenchmarkRunner> _benchmarkRunners =
        new()
        {
            [MPathRunner] = new MPathMazeBenchmarkRunner(),
            [RoyTRunner] = new RoyTAStarMazeBenchmarkRunner(),
            [AStarLiteRunner] = new AStarLiteBenchmarkRunner(),
            [LinqToAStarRunner] = new LinqToAStarMazeBenchmarkRunner(),
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

    [Benchmark] public void MPath() => _benchmarkRunners[MPathRunner].FindPath(_start, _destination);
    [Benchmark] public void RoyTAStar() => _benchmarkRunners[RoyTRunner].FindPath(_start, _destination);
    [Benchmark] public void AStarLite() => _benchmarkRunners[AStarLiteRunner].FindPath(_start, _destination);
    [Benchmark] public void LinqToAStar() => _benchmarkRunners[LinqToAStarRunner].FindPath(_start, _destination);
}
