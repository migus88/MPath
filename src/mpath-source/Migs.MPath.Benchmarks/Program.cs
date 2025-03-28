using System;
using Migs.MPath.Benchmarks;
using Migs.MPath.Benchmarks.MazeBenchmarks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;

var config = ManualConfig.CreateMinimumViable()
    .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));

if (args.Length == 0)
{
    throw new ArgumentException("Please provide a valid argument.");
}

if (args[0] == "profile")
{
    var benchmarkRunner = new MPathMazeBenchmarkRunner();
    benchmarkRunner.Init(null);

    for (var i = 0; i < 100; i++)
    {
        Console.WriteLine("Running iteration: " + i);
        benchmarkRunner.FindPath((10, 10), (502, 374));
    }
}
else if (args[0] == "debug")
{
    var benchmarkRunner = new MPathMazeBenchmarkRunner();
    benchmarkRunner.Init(null);
    benchmarkRunner.FindPath((10, 10), (502, 374));
}
else if (args[0] == "render")
{
    if (args.Length == 1)
    {
        Console.WriteLine("Rendering all paths for visual comparison...");
        var runner = new MazeBenchmarkRunner();
        runner.PrintAllResults();
        Console.WriteLine("All paths rendered successfully. Check the Results directory for output images.");
        return;
    }
    
    switch (args[1])
    {
        case "smoothing":
            new PathSmoothingBenchmarkRunner().RenderPaths();
            break;
        default:
            Console.WriteLine("Invalid argument.");
            break;
    }
}
else if (args[0] == "info")
{
    switch (args[1])
    {
        case "smoothing":
            new PathSmoothingBenchmarkRunner().PrintPathCounts();
            break;
        default:
            Console.WriteLine("Invalid argument.");
            break;
    }
}
else if (args[0] == "benchmark")
{
    if (args.Length == 1)
    {
        BenchmarkRunner.Run<MazeBenchmarkRunner>(config);
        return;
    }

    switch (args[1])
    {
        case "smoothing":
            BenchmarkRunner.Run<PathSmoothingBenchmarkRunner>(config);
            break;
        case "internal":
            BenchmarkRunner.Run<InternalBenchmarkRunner>(config);
            break;
        default:
            Console.WriteLine("Invalid argument.");
            break;
    }
}
else
{
}