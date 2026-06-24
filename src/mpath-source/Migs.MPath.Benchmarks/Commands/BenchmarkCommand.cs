using System.ComponentModel;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Migs.MPath.Benchmarks.Suites;
using Perfolizer.Horology;
using Spectre.Console.Cli;

namespace Migs.MPath.Benchmarks.Commands;

/// <summary>
/// Runs one of the BenchmarkDotNet suites. Defaults to the maze comparison.
/// </summary>
public sealed class BenchmarkCommand : Command<BenchmarkCommand.Settings>
{
    public enum BenchmarkSuite
    {
        /// <summary>MPath vs. other A* libraries over the cavern maze.</summary>
        Maze,

        /// <summary>The path-smoothing methods compared against each other.</summary>
        Smoothing,

        /// <summary>MPath internal micro-benchmarks (construction, short/long searches).</summary>
        Internal,

        /// <summary>The GetReachable movement-range flood fill at varying budgets.</summary>
        Reachability,
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[suite]")]
        [Description("Which suite to run: maze (default), smoothing, internal or reachability.")]
        [DefaultValue(BenchmarkSuite.Maze)]
        public BenchmarkSuite Suite { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var config = ManualConfig.CreateMinimumViable()
            .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));

        switch (settings.Suite)
        {
            case BenchmarkSuite.Maze:
                BenchmarkRunner.Run<MazeBenchmarkRunner>(config);
                break;
            case BenchmarkSuite.Smoothing:
                BenchmarkRunner.Run<PathSmoothingBenchmarkRunner>(config);
                break;
            case BenchmarkSuite.Internal:
                BenchmarkRunner.Run<InternalBenchmarkRunner>(config);
                break;
            case BenchmarkSuite.Reachability:
                BenchmarkRunner.Run<ReachabilityBenchmarkRunner>(config);
                break;
        }

        return 0;
    }
}
