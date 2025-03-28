using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Migs.MPath.Core;
using Migs.MPath.Core.Data;
using Migs.MPath.Core.Interfaces;
using Migs.MPath.Tools;

namespace Migs.MPath.Benchmarks
{
    [MemoryDiagnoser(false)]
    public class PathSmoothingBenchmarkRunner
    {
        private readonly Coordinate _start = new(10, 10);
        private readonly Coordinate _destination = new(502, 374);
        
        private readonly Pathfinder _pathfinderNoSmoothing;
        private readonly Pathfinder _pathfinderSimpleSmoothing;
        private readonly Pathfinder _pathfinderStringPullingSmoothing;
        private readonly IAgent _agent;

        public PathSmoothingBenchmarkRunner()
        {
            var maze = new Maze("Mazes/cavern.png");
            _agent = new Agent();

            _pathfinderNoSmoothing = new Pathfinder(maze.Cells,
                new PathfinderSettings { PathSmoothingMethod = PathSmoothingMethod.None });
            
            _pathfinderSimpleSmoothing = new Pathfinder(maze.Cells,
                new PathfinderSettings { PathSmoothingMethod = PathSmoothingMethod.Simple });
            
            _pathfinderStringPullingSmoothing = new Pathfinder(maze.Cells,
                new PathfinderSettings { PathSmoothingMethod = PathSmoothingMethod.StringPulling });
        }

        [Benchmark(Baseline = true)]
        public void NoSmoothing() => RunBenchmark(_pathfinderNoSmoothing);

        [Benchmark]
        public void SimpleSmoothing() => RunBenchmark(_pathfinderSimpleSmoothing);

        [Benchmark]
        public void StringPullingSmoothing() => RunBenchmark(_pathfinderStringPullingSmoothing);

        private void RunBenchmark(Pathfinder pathfinder)
        {
            var result = pathfinder.GetPath(_agent, _start, _destination);

            if (!result.IsSuccess)
            {
                throw new Exception("Path not found");
            }
        }
    }
}