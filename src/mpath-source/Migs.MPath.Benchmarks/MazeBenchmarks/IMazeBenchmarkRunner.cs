using Migs.MPath.Tools;

namespace Migs.MPath.Benchmarks.MazeBenchmarks;

public interface IMazeBenchmarkRunner
{
    void Init(Maze maze);
    void FindPath((int x, int y) start, (int x, int y) destination);
    void RenderPath((int x, int y) start, (int x, int y) destination);
}