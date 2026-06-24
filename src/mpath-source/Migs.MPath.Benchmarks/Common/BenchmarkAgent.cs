using Migs.MPath.Core.Interfaces;

namespace Migs.MPath.Benchmarks.Common;

/// <summary>
/// A minimal single-cell agent (Size 1) shared by every benchmark suite and competitor runner.
/// </summary>
public sealed class BenchmarkAgent : IAgent
{
    public int Size => 1;
}
