using System;
using System.IO;
using Migs.MPath.Core.Data;

namespace Migs.MPath.Benchmarks.Common;

/// <summary>
/// Shared fixture used across the benchmark suites: the cavern maze and the canonical
/// start/destination coordinates that define the long cross-maze path used for comparison.
/// </summary>
public static class BenchmarkScenario
{
    /// <summary>
    /// Absolute path to the maze image. Resolved against the assembly's base directory (where the
    /// <c>Mazes/</c> content is copied) so every command works regardless of the current directory.
    /// </summary>
    public static readonly string MazePath = Path.Combine(AppContext.BaseDirectory, "Mazes", "cavern.png");

    /// <summary>
    /// Directory where rendered result images are written. Resolved against the assembly's base
    /// directory so output lands next to the binaries instead of polluting the current directory.
    /// </summary>
    public static readonly string ResultsDirectory = Path.Combine(AppContext.BaseDirectory, "Results");

    /// <summary>The origin of the canonical long path (and the reachability flood-fill).</summary>
    public static readonly Coordinate Start = new(10, 10);

    /// <summary>The destination of the canonical long path.</summary>
    public static readonly Coordinate Destination = new(502, 374);

    /// <summary>Builds the full path for a rendered result image with the given name (no extension).</summary>
    public static string ResultImagePath(string name) => Path.Combine(ResultsDirectory, $"{name}.png");
}
