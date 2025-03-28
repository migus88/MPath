using BenchmarkDotNet.Attributes;
using Migs.MPath.Benchmarks.MazeBenchmarks;

namespace Migs.MPath.Benchmarks;

[MemoryDiagnoser(false)]
public class MazeBenchmarkRunner
{
    private readonly (int x, int y) _start = (10, 10);
    private readonly (int x, int y) _destination = (502, 374);
        
    private static readonly string AtomicRunner = nameof(MPathMazeBenchmarkRunner);
    private static readonly string RoyTRunner = nameof(RoyTAStarMazeBenchmarkRunner);
    private static readonly string AStarLiteRunner = nameof(AStarLiteBenchmarkRunner);
    private static readonly string LinqToAStarRunner = nameof(LinqToAStarMazeBenchmarkRunner);

    private readonly Dictionary<string, IMazeBenchmarkRunner> _benchmarkRunners =
        new()
        {
            [AtomicRunner] = new MPathMazeBenchmarkRunner(),
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

    [Benchmark] public void MPath() => _benchmarkRunners[AtomicRunner].FindPath(_start, _destination);
    [Benchmark] public void RoyTAStar() => _benchmarkRunners[RoyTRunner].FindPath(_start, _destination);
    [Benchmark] public void AStarLite() => _benchmarkRunners[AStarLiteRunner].FindPath(_start, _destination);
    
    // This one is so slow that it's not included in the benchmark
    [Benchmark] public void LinqToAStar() => _benchmarkRunners[LinqToAStarRunner].FindPath(_start, _destination);
}