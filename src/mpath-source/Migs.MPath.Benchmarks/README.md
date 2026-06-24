# Migs.MPath.Benchmarks

[BenchmarkDotNet](https://benchmarkdotnet.org/) benchmarks for MPath, with a
[Spectre.Console](https://spectreconsole.net/) command-line front end.

## Quick start

Run from the repository root and pass a command (use `--help` to list them all):

```bash
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- --help
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark            # maze comparison (default)
dotnet run -c Release --project src/mpath-source/Migs.MPath.Benchmarks -- benchmark reachability
```

## Commands

| Command                  | Description                                                       |
|--------------------------|-------------------------------------------------------------------|
| `benchmark [suite]`      | Run a suite: `maze` (default), `smoothing`, `internal`, `reachability`. |
| `render [target]`        | Render result images to `Results/`: `maze` (default) or `smoothing`. |
| `info [target]`          | Print stats without benchmarking (`smoothing` path lengths).      |

## Layout

```
Commands/      Spectre.Console.Cli command classes (one per CLI verb)
Suites/        The BenchmarkDotNet [Benchmark] classes
Competitors/   Adapters for rival A* libraries compared in the maze suite
Common/        Shared BenchmarkAgent + BenchmarkScenario (maze + fixed coordinates)
Mazes/         Maze image(s) used as benchmark input
Program.cs     Thin entry point that wires up the commands
```

See [docs/benchmarks/README.md](../../../docs/benchmarks/README.md) for results, methodology, and the
benchmark environment.
