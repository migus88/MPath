using System;
using Migs.MPath.Benchmarks;
using Migs.MPath.Benchmarks.MazeBenchmarks;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;

var config = ManualConfig.CreateMinimumViable()
    .WithSummaryStyle(SummaryStyle.Default.WithTimeUnit(TimeUnit.Millisecond));

if (args.Length > 0 && args[0] == "profile")
{
    var benchmarkRunner = new MigsMazeBenchmarkRunner();
    benchmarkRunner.Init(null);

    for (var i = 0; i < 100; i++)
    {
        Console.WriteLine("Running iteration: " + i);

        benchmarkRunner.FindPath((10, 10), (502, 374));
    }
}
else if (args.Length > 0 && args[0] == "internal")
{
    BenchmarkRunner.Run<InternalBenchmarkRunner>(config);
}
else if (args.Length > 0 && args[0] == "render")
{
    Console.WriteLine("Rendering all paths for visual comparison...");
    var runner = new MazeBenchmarkRunner();
    runner.PrintAllResults();
    Console.WriteLine("All paths rendered successfully. Check the Results directory for output images.");
}
else
{
    BenchmarkRunner.Run<MazeBenchmarkRunner>(config);
}