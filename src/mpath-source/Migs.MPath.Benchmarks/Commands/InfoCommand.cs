using System.ComponentModel;
using Migs.MPath.Benchmarks.Suites;
using Spectre.Console.Cli;

namespace Migs.MPath.Benchmarks.Commands;

/// <summary>
/// Prints informational statistics about a suite without running a full benchmark.
/// </summary>
public sealed class InfoCommand : Command<InfoCommand.Settings>
{
    public enum InfoTarget
    {
        /// <summary>Print the resulting path length for each smoothing method.</summary>
        Smoothing,
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[target]")]
        [Description("Which stats to print. Currently only 'smoothing' is supported.")]
        [DefaultValue(InfoTarget.Smoothing)]
        public InfoTarget Target { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        switch (settings.Target)
        {
            case InfoTarget.Smoothing:
                new PathSmoothingBenchmarkRunner().PrintPathCounts();
                break;
        }

        return 0;
    }
}
