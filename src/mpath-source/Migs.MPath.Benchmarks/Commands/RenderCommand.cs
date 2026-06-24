using System.ComponentModel;
using Migs.MPath.Benchmarks.Suites;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Migs.MPath.Benchmarks.Commands;

/// <summary>
/// Renders benchmark result images into the <c>Results/</c> folder for visual comparison.
/// </summary>
public sealed class RenderCommand : Command<RenderCommand.Settings>
{
    public enum RenderTarget
    {
        /// <summary>Render every maze runner's path (the head-to-head comparison images).</summary>
        Maze,

        /// <summary>Render the path produced by each smoothing method.</summary>
        Smoothing,
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[target]")]
        [Description("What to render: maze (default) or smoothing.")]
        [DefaultValue(RenderTarget.Maze)]
        public RenderTarget Target { get; init; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        switch (settings.Target)
        {
            case RenderTarget.Maze:
                AnsiConsole.MarkupLine("Rendering all maze paths for visual comparison...");
                new MazeBenchmarkRunner().PrintAllResults();
                break;
            case RenderTarget.Smoothing:
                AnsiConsole.MarkupLine("Rendering smoothed paths...");
                new PathSmoothingBenchmarkRunner().RenderPaths();
                break;
        }

        AnsiConsole.MarkupLine("[green]Done.[/] Check the [yellow]Results/[/] directory for output images.");
        return 0;
    }
}
