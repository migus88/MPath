using Migs.MPath.Benchmarks.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("mpath-benchmarks");

    config.AddCommand<BenchmarkCommand>("benchmark")
        .WithDescription("Run a BenchmarkDotNet suite (maze, smoothing, internal, reachability).")
        .WithExample("benchmark")
        .WithExample("benchmark", "reachability");

    config.AddCommand<RenderCommand>("render")
        .WithDescription("Render result images to the Results/ folder for visual comparison.")
        .WithExample("render")
        .WithExample("render", "smoothing");

    config.AddCommand<InfoCommand>("info")
        .WithDescription("Print informational stats for a suite (e.g. smoothed path lengths).")
        .WithExample("info", "smoothing");
});

return app.Run(args);
