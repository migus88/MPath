# MPath Unity Benchmarks

This directory contains benchmarks for comparing the performance of MPath with other pathfinding solutions using Unity's Performance Testing Framework.

## Setup

The benchmarks are already set up and ready to run. The `com.unity.test-framework.performance` package is required and should already be installed in the project.

## Running Benchmarks

### From the Editor Menu

The benchmarks can be run from the Unity Editor menu:

1. In the Unity Editor, go to `Tools > Benchmarks` 
2. Select one of the following benchmark options:
   - `Run All Benchmarks`: Runs all pathfinding benchmarks on all maze images
   - `Run MPath Benchmarks`: Runs only MPath benchmarks 
   - `Run RoyT A* Benchmarks`: Runs only RoyT.AStar benchmarks
   - `Run AStar.Lite Benchmarks`: Runs only AStar.Lite benchmarks

### From the Test Runner

You can also run the benchmarks from Unity's Test Runner window:

1. In the Unity Editor, go to `Window > General > Test Runner`
2. In the Test Runner window, make sure the "PlayMode" tab is selected
3. Find the `Benchmarks.Editor.PerformanceBenchmarks` suite
4. Run individual benchmarks or the entire suite

## Viewing Results

Once benchmarks have been run, you can view the results in several ways:

1. In the Unity Editor, go to `Tools > Benchmarks > View Results` to open the benchmark visualization window
2. You can export the results to CSV by clicking the "Export CSV" button in the visualization window
   or by using `Tools > Benchmarks > Export Results`

## Adding New Maze Images

You can add new maze images to benchmark by:

1. Place PNG images in the `Assets/Benchmarks/Mazes` directory
2. The benchmarks will automatically detect and use any PNG, JPG or GIF images in this directory

## How It Works

The benchmarks work by:

1. Finding all maze images in the Mazes directory
2. Loading each maze into a `UnityMaze` data structure
3. Running each pathfinding algorithm on the maze multiple times
4. Measuring the performance using Unity's Performance Testing Framework
5. Reporting the results

## Benchmark Parameters

The benchmarks are configured with the following parameters:

- **Start Point**: (10, 10)
- **Destination**: (502, 374)
- **Warmup Count**: 3 runs (not measured)
- **Measurement Count**: 10 runs (measured)

## Available Pathfinding Implementations

The benchmarks compare the following pathfinding implementations:

1. **MPath**: Migs.MPath custom pathfinding solution
2. **RoyT.AStar**: External A* implementation
3. **AStar.Lite**: Another external A* implementation

## Adding New Pathfinders

To add a new pathfinder implementation to the benchmarks:

1. Create a new runner class in the `Benchmarks.Editor.Runners` namespace that inherits from `BaseMazeBenchmarkRunner`
2. Implement the required `FindPath` method
3. Add the runner to the array in the `RunAllBenchmarksOnMaze` method in `PerformanceBenchmarks.cs` 