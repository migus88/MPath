using Migs.MPath.Benchmarks.Common;
using Migs.MPath.Tools;

namespace Migs.MPath.Benchmarks.Competitors;

public abstract class BaseMazeBenchmarkRunner : IMazeBenchmarkRunner
{
    protected abstract string ResultImageName { get; }

    protected Maze _maze;

    public virtual void Init(Maze maze)
    {
        _maze = maze ?? new Maze(BenchmarkScenario.MazePath);
    }

    public abstract void FindPath((int x, int y) start, (int x, int y) destination);

    public abstract void RenderPath((int x, int y) start, (int x, int y) destination);

    protected void SaveMazeResultAsImage()
    {
        Directory.CreateDirectory(BenchmarkScenario.ResultsDirectory);
        _maze.SaveImage(BenchmarkScenario.ResultImagePath(ResultImageName), 4);
    }
}