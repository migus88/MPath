using BenchmarkDotNet.Attributes;
using Migs.MPath.Benchmarks.Common;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using Migs.MPath.Tools;

namespace Migs.MPath.Benchmarks.Suites;

/// <summary>
/// Compares the path-smoothing methods (<see cref="PathSmoothingMethod.None"/>, Simple and
/// StringPulling) over the same maze and path, measuring both runtime and the resulting path length.
/// </summary>
[MemoryDiagnoser(false)]
public class PathSmoothingBenchmarkRunner
{
    private readonly Coordinate _start = BenchmarkScenario.Start;
    private readonly Coordinate _destination = BenchmarkScenario.Destination;

    private readonly Pathfinder _pathfinderNoSmoothing;
    private readonly Pathfinder _pathfinderSimpleSmoothing;
    private readonly Pathfinder _pathfinderStringPullingSmoothing;
    private readonly IAgent _agent;

    public PathSmoothingBenchmarkRunner()
    {
        var maze = new Maze(BenchmarkScenario.MazePath);
        _agent = new BenchmarkAgent();

        _pathfinderNoSmoothing = new Pathfinder(maze.Cells,
            new PathfinderSettings { PathSmoothingMethod = PathSmoothingMethod.None });

        _pathfinderSimpleSmoothing = new Pathfinder(maze.Cells,
            new PathfinderSettings { PathSmoothingMethod = PathSmoothingMethod.Simple });

        _pathfinderStringPullingSmoothing = new Pathfinder(maze.Cells,
            new PathfinderSettings { PathSmoothingMethod = PathSmoothingMethod.StringPulling });
    }

    public void PrintPathCounts()
    {
        NoSmoothingCount();
        SimpleSmoothingCount();
        StringPullingSmoothingCount();
    }

    public void RenderPaths()
    {
        using var result = NoSmoothingCount();
        RenderPath(result, "NoSmoothing");

        using var result2 = SimpleSmoothingCount();
        RenderPath(result2, "SimpleSmoothing");

        using var result3 = StringPullingSmoothingCount();
        RenderPath(result3, "StringPullingSmoothing");
    }

    private void RenderPath(PathResult result, string name)
    {
        Directory.CreateDirectory(BenchmarkScenario.ResultsDirectory);
        var maze = new Maze(BenchmarkScenario.MazePath);
        maze.AddPath(result.Path.ToArray());
        maze.SaveImage(BenchmarkScenario.ResultImagePath(name), 4);
    }

    private PathResult NoSmoothingCount()
    {
        var result = RunBenchmark(_pathfinderNoSmoothing);
        Console.WriteLine($"No Smoothing Count: {result.Path.Count()}");
        return result;
    }

    private PathResult SimpleSmoothingCount()
    {
        var result = RunBenchmark(_pathfinderSimpleSmoothing);
        Console.WriteLine($"Simple Smoothing Count: {result.Path.Count()}");
        return result;
    }

    private PathResult StringPullingSmoothingCount()
    {
        var result = RunBenchmark(_pathfinderStringPullingSmoothing);
        Console.WriteLine($"String Pulling Smoothing Count: {result.Path.Count()}");
        return result;
    }

    [Benchmark(Baseline = true)]
    public void NoSmoothing() => RunBenchmark(_pathfinderNoSmoothing);

    [Benchmark]
    public void SimpleSmoothing() => RunBenchmark(_pathfinderSimpleSmoothing);

    [Benchmark]
    public void StringPullingSmoothing() => RunBenchmark(_pathfinderStringPullingSmoothing);

    private PathResult RunBenchmark(Pathfinder pathfinder)
    {
        var result = pathfinder.GetPath(_agent, _start, _destination);

        if (!result.IsSuccess)
        {
            throw new Exception("Path not found");
        }

        return result;
    }
}
